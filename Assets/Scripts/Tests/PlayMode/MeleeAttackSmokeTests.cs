using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;
using SurvivalRPG.Abilities;
using SurvivalRPG.Survival;
using SurvivalRPG.UI;

namespace SurvivalRPG.Tests.PlayMode
{
    /// <summary>
    /// PlayMode smoke tests for the melee attack interaction:
    /// clicking an EnemyAI calls AttackEnemyServerRpc which calls EnemyAI.TakeDamage.
    /// </summary>
    public class MeleeAttackSmokeTests
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
            // Clear EnemyTargetTracker listeners
            EnemyTargetTracker.ClearAllListeners();
            NGOTestHelper.CleanUp();
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyTakeDamage_ReducesHealth_AndDiesAtZero()
        {
            // Arrange — spawn an enemy on the server (host)
            var enemyGo = new GameObject("TestEnemy");
            var enemyNetObj = enemyGo.AddComponent<NetworkObject>();
            var enemyAttributes = enemyGo.AddComponent<AttributeSet>();
            var enemyAI = enemyGo.AddComponent<EnemyAI>();
            enemyGo.AddComponent<SphereCollider>();

            enemyNetObj.Spawn();
            yield return null;

            float initialHealth = enemyAttributes.Health.Value;
            Assert.Greater(initialHealth, 0f, "Enemy should start with health > 0.");

            bool dyingEventFired = false;
            EnemyAI.OnEnemyDied += (e) =>
            {
                if (e == enemyAI) dyingEventFired = true;
            };

            // Act — deal damage below the death threshold
            Debug.Log("[Test] Dealing 10 damage to enemy...");
            enemyAI.TakeDamage(10f);
            yield return new WaitForSeconds(0.1f);

            // Assert — health reduced
            Assert.AreEqual(initialHealth - 10f, enemyAttributes.Health.Value,
                "Health should be reduced by damage amount.");
            Assert.IsFalse(dyingEventFired, "Enemy should not have died from 10 damage.");

            // Act — deal lethal damage
            Debug.Log("[Test] Dealing lethal damage to enemy...");
            enemyAI.TakeDamage(enemyAttributes.Health.Value + 1f);
            yield return new WaitForSeconds(0.2f);

            // Assert — enemy died
            Assert.IsTrue(dyingEventFired, "OnEnemyDied should have fired after lethal damage.");
            Debug.Log("[Test] MeleeAttack smoke test passed.");
        }

        [UnityTest]
        public IEnumerator EnemyTargetTracker_SetTarget_FiresEvent()
        {
            // Arrange
            var enemyGo = new GameObject("Target");
            var enemyNetObj = enemyGo.AddComponent<NetworkObject>();
            var enemyAI = enemyGo.AddComponent<EnemyAI>();
            enemyGo.AddComponent<AttributeSet>();
            enemyGo.AddComponent<SphereCollider>();
            enemyNetObj.Spawn();
            yield return null;

            EnemyAI receivedTarget = null;
            EnemyTargetTracker.OnEnemyTargeted += (e) => receivedTarget = e;

            // Act
            EnemyTargetTracker.SetTarget(enemyAI);
            yield return null;

            // Assert
            Assert.AreEqual(enemyAI, receivedTarget, "EnemyTargetTracker should broadcast the correct EnemyAI.");
            Debug.Log("[Test] EnemyTargetTracker test passed.");
        }
    }
}
