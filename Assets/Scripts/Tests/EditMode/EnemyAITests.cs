using NUnit.Framework;
using UnityEngine;
using ByteWar.Core;
using ByteWar.Survival;
using ByteWar.Abilities;

namespace ByteWar.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for EnemyAI line-of-sight and attack logic.
    /// </summary>
    [TestFixture]
    public class EnemyAITests
    {
        [Test]
        public void EnemyAI_HasRequiredComponents()
        {
            var go = new GameObject("TestEnemy");
            go.AddComponent<AttributeSet>();
            var ai = go.AddComponent<EnemyAI>();

            Assert.IsNotNull(ai, "EnemyAI component should be attached.");
            Assert.IsNotNull(go.GetComponent<AttributeSet>(), "EnemyAI requires AttributeSet.");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void EnemyAI_ImplementsIDamageable()
        {
            var go = new GameObject("TestEnemy");
            go.AddComponent<AttributeSet>();
            var ai = go.AddComponent<EnemyAI>();

            Assert.IsTrue(ai is IDamageable, "EnemyAI should implement IDamageable.");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void EnemyAI_LOSRaycast_WallBlocksAttack()
        {
            // Setup: enemy and target with a wall between them
            var enemy = new GameObject("Enemy");
            enemy.transform.position = new Vector3(0f, 0f, 0f);

            var target = new GameObject("Target");
            target.transform.position = new Vector3(10f, 0f, 0f);

            // Place a wall between them
            var wall = new GameObject("Wall");
            wall.transform.position = new Vector3(5f, 1f, 0f);
            var wallCol = wall.AddComponent<BoxCollider>();
            wallCol.size = new Vector3(0.3f, 4f, 4f);

            // Wait for physics to register the collider
            Physics.SyncTransforms();

            // Simulate the LOS check from EnemyAI.AttackTarget
            Vector3 eyePos = enemy.transform.position + Vector3.up * 1.5f;
            Vector3 targetPos = target.transform.position + Vector3.up * 1.0f;
            Vector3 toTarget = targetPos - eyePos;
            float dist = toTarget.magnitude;

            bool blocked = false;
            if (Physics.Raycast(eyePos, toTarget.normalized, out RaycastHit hit, dist))
            {
                if (!hit.transform.IsChildOf(target.transform) && hit.transform != target.transform)
                {
                    blocked = true;
                }
            }

            Assert.IsTrue(blocked, "Wall between enemy and target should block LOS.");

            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(wall);
        }

        [Test]
        public void EnemyAI_LOSRaycast_NoWall_NotBlocked()
        {
            var enemy = new GameObject("Enemy");
            enemy.transform.position = new Vector3(0f, 0f, 0f);

            var target = new GameObject("Target");
            target.transform.position = new Vector3(5f, 0f, 0f);
            // Give target a collider so raycast can hit it
            target.AddComponent<CapsuleCollider>();

            Physics.SyncTransforms();

            Vector3 eyePos = enemy.transform.position + Vector3.up * 1.5f;
            Vector3 targetPos = target.transform.position + Vector3.up * 1.0f;
            Vector3 toTarget = targetPos - eyePos;
            float dist = toTarget.magnitude;

            bool blocked = false;
            if (Physics.Raycast(eyePos, toTarget.normalized, out RaycastHit hit, dist))
            {
                if (!hit.transform.IsChildOf(target.transform) && hit.transform != target.transform)
                {
                    blocked = true;
                }
            }

            Assert.IsFalse(blocked, "Without a wall, LOS should not be blocked.");

            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(target);
        }
    }
}
