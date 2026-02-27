using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;
using ByteWar.Core;
using ByteWar.Survival;

namespace ByteWar.Tests.PlayMode
{
    public class DestructionSmokeTests
    {
        private NetworkManager _networkManager;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _networkManager = NGOTestHelper.CreateNetworkManager();
            _networkManager.StartHost();
            yield return NGOTestHelper.WaitForLocalPlayerReady(_networkManager);
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            NGOTestHelper.CleanUp();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerInteraction_RequestDestructibleDamage_DespawnOnZeroHealth()
        {
            var playerObject = _networkManager.SpawnManager.GetLocalPlayerObject();
            Assert.IsNotNull(playerObject, "Local player should exist.");

            var interaction = playerObject.GetComponent<PlayerInteraction>();
            Assert.IsNotNull(interaction, "PlayerInteraction should be present on local player.");

            var profile = ScriptableObject.CreateInstance<DestructibleProfile>();
            profile.MaxHealth = 15f;
            profile.DamageMultipliersMutable.Add(new DamageTypeMultiplier
            {
                DamageType = DamageType.Blunt,
                Multiplier = 2f,
            });

            var destructibleObject = new GameObject("TestDestructible");
            destructibleObject.transform.position = playerObject.transform.position + Vector3.forward * 2f;
            destructibleObject.AddComponent<BoxCollider>();
            var networkObject = destructibleObject.AddComponent<NetworkObject>();
            var destructible = destructibleObject.AddComponent<DestructibleComponent>();
            destructible.SetupForTest(profile);

            networkObject.Spawn();
            yield return null;

            ulong targetNetworkObjectId = networkObject.NetworkObjectId;
            interaction.TryDamageDestructible(destructible);
            yield return new WaitForSeconds(0.2f);

            bool stillSpawned = _networkManager.SpawnManager.SpawnedObjects.ContainsKey(targetNetworkObjectId);
            Assert.IsFalse(stillSpawned, "Destructible should be despawned after lethal server-authoritative damage request.");

            Object.DestroyImmediate(profile);
            Debug.Log("[Test] Destruction request smoke test passed.");
        }

        [UnityTest]
        public IEnumerator PlayerInteraction_RequestDestructibleDamage_RejectsOutOfRangeTarget()
        {
            var playerObject = _networkManager.SpawnManager.GetLocalPlayerObject();
            Assert.IsNotNull(playerObject, "Local player should exist.");

            var interaction = playerObject.GetComponent<PlayerInteraction>();
            Assert.IsNotNull(interaction, "PlayerInteraction should be present on local player.");

            var profile = ScriptableObject.CreateInstance<DestructibleProfile>();
            profile.MaxHealth = 40f;

            var destructibleObject = new GameObject("OutOfRangeDestructible");
            float outOfRangeOffset = GameConstants.GetInteractionRange() + 25f;
            destructibleObject.transform.position = playerObject.transform.position + Vector3.forward * outOfRangeOffset;
            destructibleObject.AddComponent<BoxCollider>();
            var networkObject = destructibleObject.AddComponent<NetworkObject>();
            var destructible = destructibleObject.AddComponent<DestructibleComponent>();
            destructible.SetupForTest(profile);

            networkObject.Spawn();
            yield return null;

            float healthBefore = destructible.CurrentHealth;
            interaction.TryDamageDestructible(destructible);
            yield return new WaitForSeconds(0.2f);

            Assert.AreEqual(healthBefore, destructible.CurrentHealth, 0.0001f, "Out-of-range destruction request should be rejected by server validation.");
            Assert.IsTrue(destructible.IsSpawned, "Out-of-range target should remain spawned.");

            Object.DestroyImmediate(profile);
            Debug.Log("[Test] Out-of-range destruction validation smoke test passed.");
        }
    }
}