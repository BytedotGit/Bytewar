using System.Collections;
using ByteWar.Survival;
using UnityEngine;

namespace ByteWar.Core
{
    /// <summary>
    /// AutoTester scenario that validates the server-authoritative destructible request path:
    /// - In-range request applies damage and despawns at zero health.
    /// - Out-of-range request is rejected by server validation.
    /// </summary>
    public class AutoTestScenarioDestruction : IAutoTestScenario
    {
        private const float LethalTargetDistance = 2f;
        private const float RequestSettleSeconds = 0.25f;
        private const float OutOfRangePadding = 25f;
        private const float HealthComparisonEpsilon = 0.0001f;
        private const float LethalTargetMaxHealth = 15f;
        private const float RejectedTargetMaxHealth = 40f;

        private const int MissingPlayerQuitCode = 101;
        private const int MissingInteractionQuitCode = 102;
        private const int MissingNetworkManagerQuitCode = 103;
        private const int SpawnFailureQuitCode = 104;
        private const int LethalRequestFailureQuitCode = 105;
        private const int RejectedRequestFailureQuitCode = 106;

        public string Name => "Destruction";

        public IEnumerator Run(AutoTesterContext ctx)
        {
            Debug.Log("[AutoTester] Running Destruction scenario...");

            if (ctx.LocalPlayer == null)
            {
                Debug.LogError("[AutoTester] FAIL: Destruction - Local player is missing.");
                Application.Quit(MissingPlayerQuitCode);
                yield break;
            }

            if (ctx.NetworkManager == null || !ctx.NetworkManager.IsServer)
            {
                Debug.LogError("[AutoTester] FAIL: Destruction - NetworkManager is missing or not server-authoritative.");
                Application.Quit(MissingNetworkManagerQuitCode);
                yield break;
            }

            var interaction = ctx.LocalPlayer.GetComponent<PlayerInteraction>();
            if (interaction == null)
            {
                Debug.LogError("[AutoTester] FAIL: Destruction - PlayerInteraction missing on local player.");
                Application.Quit(MissingInteractionQuitCode);
                yield break;
            }

            var lethalProfile = ScriptableObject.CreateInstance<DestructibleProfile>();
            var rejectedProfile = ScriptableObject.CreateInstance<DestructibleProfile>();

            DestructibleComponent lethalTarget = null;
            DestructibleComponent rejectedTarget = null;

            try
            {
                lethalProfile.MaxHealth = LethalTargetMaxHealth;
                lethalProfile.DamageMultipliersMutable.Add(new DamageTypeMultiplier
                {
                    DamageType = DamageType.Blunt,
                    Multiplier = 2f,
                });

                rejectedProfile.MaxHealth = RejectedTargetMaxHealth;

                lethalTarget = SpawnDestructible(
                    "AutoTest_Destructible_Lethal",
                    ctx.LocalPlayer.transform.position + (ctx.LocalPlayer.transform.forward * LethalTargetDistance),
                    lethalProfile);

                if (lethalTarget == null || !lethalTarget.IsSpawned)
                {
                    Debug.LogError("[AutoTester] FAIL: Destruction - Failed to spawn in-range destructible target.");
                    Application.Quit(SpawnFailureQuitCode);
                    yield break;
                }

                ulong lethalTargetId = lethalTarget.NetworkObjectId;
                interaction.TryDamageDestructible(lethalTarget);
                yield return new WaitForSeconds(RequestSettleSeconds);

                bool lethalStillSpawned = ctx.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(lethalTargetId);
                if (lethalStillSpawned)
                {
                    Debug.LogError("[AutoTester] FAIL: Destruction - In-range request did not destroy/despawn target.");
                    Application.Quit(LethalRequestFailureQuitCode);
                    yield break;
                }

                Debug.Log("[AutoTester] PASS: Destruction in-range request path.");

                float outOfRangeDistance = GameConstants.GetInteractionRange() + OutOfRangePadding;
                rejectedTarget = SpawnDestructible(
                    "AutoTest_Destructible_OutOfRange",
                    ctx.LocalPlayer.transform.position + (ctx.LocalPlayer.transform.forward * outOfRangeDistance),
                    rejectedProfile);

                if (rejectedTarget == null || !rejectedTarget.IsSpawned)
                {
                    Debug.LogError("[AutoTester] FAIL: Destruction - Failed to spawn out-of-range destructible target.");
                    Application.Quit(SpawnFailureQuitCode);
                    yield break;
                }

                float healthBefore = rejectedTarget.CurrentHealth;
                interaction.TryDamageDestructible(rejectedTarget);
                yield return new WaitForSeconds(RequestSettleSeconds);

                bool healthUnchanged = Mathf.Abs(rejectedTarget.CurrentHealth - healthBefore) <= HealthComparisonEpsilon;
                if (!healthUnchanged || !rejectedTarget.IsSpawned)
                {
                    Debug.LogError(
                        $"[AutoTester] FAIL: Destruction - Out-of-range request should be rejected. " +
                        $"healthBefore={healthBefore:0.###} healthAfter={rejectedTarget.CurrentHealth:0.###} spawned={rejectedTarget.IsSpawned}");
                    Application.Quit(RejectedRequestFailureQuitCode);
                    yield break;
                }

                Debug.Log("[AutoTester] PASS: Destruction out-of-range rejection path.");
                Debug.Log("[AutoTester] PASS: Destruction scenario complete.");
            }
            finally
            {
                CleanupDestructible(lethalTarget);
                CleanupDestructible(rejectedTarget);
                Object.Destroy(lethalProfile);
                Object.Destroy(rejectedProfile);
            }

            yield return null;
        }

        private static DestructibleComponent SpawnDestructible(string name, Vector3 position, DestructibleProfile profile)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.AddComponent<BoxCollider>();
            var networkObject = go.AddComponent<Unity.Netcode.NetworkObject>();
            var destructible = go.AddComponent<DestructibleComponent>();
            destructible.SetupForTest(profile);
            networkObject.Spawn();
            return destructible;
        }

        private static void CleanupDestructible(DestructibleComponent destructible)
        {
            if (destructible == null)
                return;

            var networkObject = destructible.NetworkObject;
            if (networkObject != null && networkObject.IsSpawned)
            {
                networkObject.Despawn(true);
                return;
            }

            if (destructible.gameObject != null)
                Object.Destroy(destructible.gameObject);
        }
    }
}