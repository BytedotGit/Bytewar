using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;
using SurvivalRPG.Abilities;
using SurvivalRPG.Survival;

namespace SurvivalRPG.Tests.PlayMode
{
    public class CombatSmokeTests
    {
        private NetworkManager _networkManager;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _networkManager = NGOTestHelper.CreateNetworkManager();
            _networkManager.StartHost();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            NGOTestHelper.CleanUp();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClientCastsAbility_ServerProcessesManaAndCooldown_EnemyTakesDamage()
        {
            // Arrange
            var playerObj = _networkManager.SpawnManager.GetLocalPlayerObject();
            var abilitySystem = playerObj.GetComponent<AbilitySystemComponent>();
            var attributes = playerObj.GetComponent<AttributeSet>();

            // Setup Mock Ability
            var mockAbility = ScriptableObject.CreateInstance<MockDamageAbility>();
            mockAbility.AbilityName = "MockDamage";
            mockAbility.ManaCost = 20f;
            mockAbility.Cooldown = 5f;
            mockAbility.DamageAmount = 25f;

            abilitySystem.LearnedAbilities.Add(mockAbility);

            // Setup Enemy
            var enemyGo = new GameObject("Enemy");
            enemyGo.transform.position = new Vector3(2, 0, 0);
            var enemyNetworkObj = enemyGo.AddComponent<NetworkObject>();
            var enemyAttributes = enemyGo.AddComponent<AttributeSet>();
            var enemyAI = enemyGo.AddComponent<EnemyAI>();
            var collider = enemyGo.AddComponent<SphereCollider>();
            collider.radius = 1f;

            enemyNetworkObj.Spawn();
            yield return null;

            float initialMana = attributes.Mana.Value;
            float initialEnemyHealth = enemyAttributes.Health.Value;

            // Act
            Debug.Log("[Test] Casting MockDamageAbility...");
            Assert.IsTrue(abilitySystem.TryCastAbility(0, enemyGo.transform.position), "TryCastAbility should return true for valid cast request.");
            yield return new WaitForSeconds(0.1f); // Wait for RPCs

            // Assert
            Assert.AreEqual(initialMana - 20f, attributes.Mana.Value, "Mana was not consumed correctly.");
            Assert.IsTrue(abilitySystem.IsOnCooldown("MockDamage"), "Ability is not on cooldown.");
            Assert.AreEqual(initialEnemyHealth - 25f, enemyAttributes.Health.Value, "Enemy did not take damage.");

            Debug.Log("[Test] Combat smoke test passed successfully.");
        }

        public class MockDamageAbility : Ability
        {
            public float DamageAmount;

            public override void Execute(AbilitySystemComponent caster, Vector3 targetPosition)
            {
                Debug.Log($"[MockDamageAbility] Executing at {targetPosition}");

                // Find enemy near target position
                Collider[] colliders = Physics.OverlapSphere(targetPosition, 2f);
                foreach (var col in colliders)
                {
                    var enemyAttributes = col.GetComponent<AttributeSet>();
                    if (enemyAttributes != null && col.gameObject != caster.gameObject)
                    {
                        enemyAttributes.ApplyDamage(DamageAmount);
                        Debug.Log($"[MockDamageAbility] Applied {DamageAmount} damage to {col.gameObject.name}");
                    }
                }
            }
        }
    }
}