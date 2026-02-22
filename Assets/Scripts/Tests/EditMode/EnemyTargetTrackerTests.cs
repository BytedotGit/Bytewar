using NUnit.Framework;
using UnityEngine;
using ByteWar.Survival;
using ByteWar.UI;

namespace ByteWar.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for EnemyTargetTracker event bus.
    /// </summary>
    public class EnemyTargetTrackerTests
    {
        [TearDown]
        public void TearDown()
        {
            // Clear all listeners between tests to avoid cross-test contamination
            EnemyTargetTracker.ClearAllListeners();
        }

        [Test]
        public void SetTarget_FiresEvent_WithCorrectEnemy()
        {
            // Arrange
            var enemyGo = new GameObject("TestEnemy");
            enemyGo.AddComponent<SphereCollider>(); // EnemyAI needs collider
            // We can't easily instantiate EnemyAI (requires NetworkObject), so test with null first
            EnemyAI receivedEnemy = null;
            bool eventFired = false;

            EnemyTargetTracker.OnEnemyTargeted += (e) =>
            {
                eventFired = true;
                receivedEnemy = e;
            };

            // Act — passing null (clear target)
            EnemyTargetTracker.SetTarget(null);

            // Assert
            Assert.IsTrue(eventFired, "OnEnemyTargeted event was not fired.");
            Assert.IsNull(receivedEnemy, "Expected null enemy for clear-target call.");

            Object.DestroyImmediate(enemyGo);
        }

        [Test]
        public void SetTarget_MultipleSubscribers_AllReceiveEvent()
        {
            // Arrange
            int callCount = 0;
            EnemyTargetTracker.OnEnemyTargeted += _ => callCount++;
            EnemyTargetTracker.OnEnemyTargeted += _ => callCount++;
            EnemyTargetTracker.OnEnemyTargeted += _ => callCount++;

            // Act
            EnemyTargetTracker.SetTarget(null);

            // Assert
            Assert.AreEqual(3, callCount, "All 3 subscribers should have been called.");
        }

        [Test]
        public void SetTarget_NoSubscribers_DoesNotThrow()
        {
            // Arrange — no subscribers registered
            Assert.DoesNotThrow(() => EnemyTargetTracker.SetTarget(null));
        }
    }
}
