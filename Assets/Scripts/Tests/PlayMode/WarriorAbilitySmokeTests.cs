using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;
using ByteWar.Abilities;
using ByteWar.Abilities.Warrior;
using ByteWar.Core;
using ByteWar.Survival;

namespace ByteWar.Tests.PlayMode
{
    /// <summary>
    /// PlayMode smoke tests for the Warrior ability system.
    /// Covers CleaveAbility execution, SFX/VFX dispatch, mana consumption,
    /// cooldown tracking, and enemy damage in a live NGO host session.
    /// </summary>
    public class WarriorAbilitySmokeTests
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

        // ── CleaveAbility: Cast, Mana, Cooldown ──────────────────────────────────

        [UnityTest]
        public IEnumerator CleaveAbility_TryCast_ConsumesManaAndSetsCooldown()
        {
            var playerObj = _networkManager.SpawnManager.GetLocalPlayerObject();
            var abilitySystem = playerObj.GetComponent<AbilitySystemComponent>();
            var attributes = playerObj.GetComponent<AttributeSet>();

            Assert.IsNotNull(abilitySystem, "AbilitySystemComponent required.");
            Assert.IsTrue(abilitySystem.IsSpawned, "ASC must be network-spawned.");

            var cleave = ScriptableObject.CreateInstance<CleaveAbility>();
            cleave.AbilityName = "Iron Tide Cleave";
            cleave.ManaCost = 10f;
            cleave.Cooldown = 3f;
            cleave.Radius = 4f;
            cleave.ArcAngle = 120f;
            cleave.CastVFXType = VFXType.CleaveHit;
            cleave.CastSFXType = SFXType.CleaveHit;

            abilitySystem.AddLearnedAbility(cleave);

            float manaBefore = attributes.Mana.Value;
            Assert.IsTrue(manaBefore >= cleave.ManaCost, "Player must have enough mana to cast.");

            bool castResult = abilitySystem.TryCastAbility(0, playerObj.transform.position + playerObj.transform.forward * 2f);
            Assert.IsTrue(castResult, "TryCastAbility should succeed with sufficient mana.");

            // Wait for server RPC processing
            yield return new WaitForSeconds(0.15f);

            Assert.AreEqual(manaBefore - 10f, attributes.Mana.Value, 0.01f,
                "Mana should be reduced by the ability's mana cost.");
            Assert.IsTrue(abilitySystem.IsOnCooldown("Iron Tide Cleave"),
                "Ability should be on cooldown after casting.");

            Debug.Log("[WarriorAbilitySmokeTests] PASS: CleaveAbility consumes mana and sets cooldown.");
            Object.Destroy(cleave);
        }

        [UnityTest]
        public IEnumerator CleaveAbility_OnCooldown_CastFails()
        {
            var playerObj = _networkManager.SpawnManager.GetLocalPlayerObject();
            var abilitySystem = playerObj.GetComponent<AbilitySystemComponent>();

            var cleave = ScriptableObject.CreateInstance<CleaveAbility>();
            cleave.AbilityName = "Iron Tide Cleave";
            cleave.ManaCost = 5f;
            cleave.Cooldown = 10f; // Long cooldown to ensure still active
            cleave.Radius = 4f;
            cleave.ArcAngle = 120f;

            abilitySystem.AddLearnedAbility(cleave);

            // First cast should succeed
            bool firstCast = abilitySystem.TryCastAbility(0, Vector3.zero);
            Assert.IsTrue(firstCast, "First cast should succeed.");
            yield return new WaitForSeconds(0.15f);

            // Second cast should fail (cooldown)
            bool secondCast = abilitySystem.TryCastAbility(0, Vector3.zero);
            Assert.IsFalse(secondCast, "Second cast should fail — ability on cooldown.");

            Debug.Log("[WarriorAbilitySmokeTests] PASS: CleaveAbility blocked while on cooldown.");
            Object.Destroy(cleave);
        }

        [UnityTest]
        public IEnumerator CleaveAbility_InsufficientMana_CastFails()
        {
            var playerObj = _networkManager.SpawnManager.GetLocalPlayerObject();
            var abilitySystem = playerObj.GetComponent<AbilitySystemComponent>();
            var attributes = playerObj.GetComponent<AttributeSet>();

            var cleave = ScriptableObject.CreateInstance<CleaveAbility>();
            cleave.AbilityName = "Mana Drain Cleave";
            cleave.ManaCost = 99999f; // Impossible cost
            cleave.Cooldown = 1f;
            cleave.Radius = 4f;
            cleave.ArcAngle = 120f;

            abilitySystem.AddLearnedAbility(cleave);

            bool castResult = abilitySystem.TryCastAbility(0, Vector3.zero);
            Assert.IsFalse(castResult, "Cast should fail when mana is insufficient.");

            Debug.Log("[WarriorAbilitySmokeTests] PASS: CleaveAbility fails without sufficient mana.");
            yield return null;
            Object.Destroy(cleave);
        }

        // ── CleaveAbility: Enemy Damage ───────────────────────────────────────────

        [UnityTest]
        public IEnumerator CleaveAbility_DamagesEnemyInArc()
        {
            var playerObj = _networkManager.SpawnManager.GetLocalPlayerObject();
            var abilitySystem = playerObj.GetComponent<AbilitySystemComponent>();
            playerObj.transform.position = Vector3.zero;
            playerObj.transform.rotation = Quaternion.LookRotation(Vector3.forward);

            // Create damage effect
            var damageEffect = ScriptableObject.CreateInstance<GameplayEffect>();
            damageEffect.EffectName = "Cleave Damage";
            damageEffect.Type = EffectType.Damage;
            damageEffect.DurationType = DurationType.Instant;
            damageEffect.Magnitude = 25f;

            var cleave = ScriptableObject.CreateInstance<CleaveAbility>();
            cleave.AbilityName = "Test Cleave";
            cleave.ManaCost = 5f;
            cleave.Cooldown = 1f;
            cleave.Radius = 6f;
            cleave.ArcAngle = 120f;
            cleave.DamageEffect = damageEffect;
            cleave.CastVFXType = VFXType.CleaveHit;
            cleave.CastSFXType = SFXType.CleaveHit;

            abilitySystem.AddLearnedAbility(cleave);

            // Spawn an enemy in front of the player (inside the arc)
            var enemyGo = new GameObject("TestEnemy");
            enemyGo.transform.position = new Vector3(0, 0, 3f); // Directly in front
            var enemyNet = enemyGo.AddComponent<NetworkObject>();
            var enemyAttributes = enemyGo.AddComponent<AttributeSet>();
            var enemyAI = enemyGo.AddComponent<EnemyAI>();
            var col = enemyGo.AddComponent<SphereCollider>();
            col.radius = 1f;
            enemyNet.Spawn();
            yield return null;

            float healthBefore = enemyAttributes.Health.Value;
            Assert.Greater(healthBefore, 0f, "Enemy should start with positive health.");

            bool castResult = abilitySystem.TryCastAbility(0, playerObj.transform.position);
            Assert.IsTrue(castResult, "Cast should succeed with valid mana.");
            yield return new WaitForSeconds(0.15f);

            float healthAfter = enemyAttributes.Health.Value;
            Assert.AreEqual(healthBefore - 25f, healthAfter, 0.01f,
                "Enemy in front of player should take cleave damage.");

            Debug.Log($"[WarriorAbilitySmokeTests] PASS: Enemy health {healthBefore} -> {healthAfter}.");
            Object.Destroy(damageEffect);
            Object.Destroy(cleave);
        }

        [UnityTest]
        public IEnumerator CleaveAbility_DoesNotDamageEnemyBehind()
        {
            var playerObj = _networkManager.SpawnManager.GetLocalPlayerObject();
            var abilitySystem = playerObj.GetComponent<AbilitySystemComponent>();
            playerObj.transform.position = Vector3.zero;
            playerObj.transform.rotation = Quaternion.LookRotation(Vector3.forward);

            var damageEffect = ScriptableObject.CreateInstance<GameplayEffect>();
            damageEffect.EffectName = "Cleave Damage";
            damageEffect.Type = EffectType.Damage;
            damageEffect.DurationType = DurationType.Instant;
            damageEffect.Magnitude = 25f;

            var cleave = ScriptableObject.CreateInstance<CleaveAbility>();
            cleave.AbilityName = "Behind Check Cleave";
            cleave.ManaCost = 5f;
            cleave.Cooldown = 1f;
            cleave.Radius = 6f;
            cleave.ArcAngle = 120f;
            cleave.DamageEffect = damageEffect;

            abilitySystem.AddLearnedAbility(cleave);

            // Spawn enemy behind the player (outside the forward arc)
            var enemyGo = new GameObject("EnemyBehind");
            enemyGo.transform.position = new Vector3(0, 0, -3f); // Behind
            var enemyNet = enemyGo.AddComponent<NetworkObject>();
            var enemyAttributes = enemyGo.AddComponent<AttributeSet>();
            var enemyAI = enemyGo.AddComponent<EnemyAI>();
            var col = enemyGo.AddComponent<SphereCollider>();
            col.radius = 1f;
            enemyNet.Spawn();
            yield return null;

            float healthBefore = enemyAttributes.Health.Value;

            bool castResult = abilitySystem.TryCastAbility(0, playerObj.transform.position);
            Assert.IsTrue(castResult, "Cast should succeed.");
            yield return new WaitForSeconds(0.15f);

            float healthAfter = enemyAttributes.Health.Value;
            Assert.AreEqual(healthBefore, healthAfter, 0.01f,
                "Enemy behind the player should NOT take cleave damage.");

            Debug.Log("[WarriorAbilitySmokeTests] PASS: Enemy behind player was not damaged.");
            Object.Destroy(damageEffect);
            Object.Destroy(cleave);
        }

        // ── Audio/VFX dispatch ────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator CleaveAbility_Cast_PlaysAudioWithoutException()
        {
            // Ensure AudioManager singleton exists
            var audioGo = new GameObject("AudioManager");
            audioGo.AddComponent<AudioManager>();
            yield return null;

            var playerObj = _networkManager.SpawnManager.GetLocalPlayerObject();
            var abilitySystem = playerObj.GetComponent<AbilitySystemComponent>();

            var cleave = ScriptableObject.CreateInstance<CleaveAbility>();
            cleave.AbilityName = "Audio Test Cleave";
            cleave.ManaCost = 5f;
            cleave.Cooldown = 1f;
            cleave.Radius = 4f;
            cleave.ArcAngle = 120f;
            cleave.CastSFXType = SFXType.CleaveHit;
            cleave.CastVFXType = VFXType.CleaveHit;

            abilitySystem.AddLearnedAbility(cleave);

            Assert.DoesNotThrow(() =>
            {
                abilitySystem.TryCastAbility(0, playerObj.transform.position);
            }, "Casting CleaveAbility should not throw even without loaded audio clips.");

            yield return new WaitForSeconds(0.15f);

            // Verify AudioManager is still intact
            Assert.IsNotNull(AudioManager.Instance, "AudioManager singleton should survive ability cast.");

            Debug.Log("[WarriorAbilitySmokeTests] PASS: CleaveAbility audio dispatch no exceptions.");
            Object.Destroy(cleave);
            Object.Destroy(audioGo);
        }

        [UnityTest]
        public IEnumerator CleaveAbility_Cast_PlaysVFXWithoutException()
        {
            // Create a VFXManager (requires NetworkObject)
            var vfxGo = new GameObject("VFXManager");
            vfxGo.AddComponent<NetworkObject>();
            var vfxManager = vfxGo.AddComponent<VFXManager>();
            yield return null;

            var playerObj = _networkManager.SpawnManager.GetLocalPlayerObject();
            var abilitySystem = playerObj.GetComponent<AbilitySystemComponent>();

            var cleave = ScriptableObject.CreateInstance<CleaveAbility>();
            cleave.AbilityName = "VFX Test Cleave";
            cleave.ManaCost = 5f;
            cleave.Cooldown = 1f;
            cleave.Radius = 4f;
            cleave.ArcAngle = 120f;
            cleave.CastSFXType = SFXType.CleaveHit;
            cleave.CastVFXType = VFXType.CleaveHit;

            abilitySystem.AddLearnedAbility(cleave);

            Assert.DoesNotThrow(() =>
            {
                abilitySystem.TryCastAbility(0, playerObj.transform.position);
            }, "Casting CleaveAbility should not throw even without VFX prefabs.");

            yield return new WaitForSeconds(0.15f);

            Debug.Log("[WarriorAbilitySmokeTests] PASS: CleaveAbility VFX dispatch no exceptions.");
            Object.Destroy(cleave);
            Object.Destroy(vfxGo);
        }

        // ── AbilitySystemComponent: index bounds ──────────────────────────────────

        [UnityTest]
        public IEnumerator AbilitySystem_InvalidIndex_DoesNotThrow()
        {
            var playerObj = _networkManager.SpawnManager.GetLocalPlayerObject();
            var abilitySystem = playerObj.GetComponent<AbilitySystemComponent>();

            Assert.DoesNotThrow(() =>
            {
                bool result = abilitySystem.TryCastAbility(-1, Vector3.zero);
                Assert.IsFalse(result, "Negative index should return false.");
            });

            Assert.DoesNotThrow(() =>
            {
                bool result = abilitySystem.TryCastAbility(999, Vector3.zero);
                Assert.IsFalse(result, "Out-of-range index should return false.");
            });

            Debug.Log("[WarriorAbilitySmokeTests] PASS: Invalid ability index handled gracefully.");
            yield return null;
        }
    }
}
