using UnityEngine;
using Unity.Netcode;
using ByteWar.Survival;

namespace ByteWar.Abilities.Mage
{
    [CreateAssetMenu(fileName = "Fireball", menuName = "ByteWar/Abilities/Mage/Fireball")]
    public class FireballAbility : Ability
    {
        public GameObject ProjectilePrefab;
        public float ProjectileSpeed = 20f;
        public GameplayEffect DamageEffect;

        public override void Execute(AbilitySystemComponent caster, Vector3 targetPosition)
        {
            if (ProjectilePrefab == null)
            {
                Debug.LogError("[FireballAbility] ProjectilePrefab is not assigned on the FireballAbility asset!");
                return;
            }

            if (!caster.IsServer)
            {
                Debug.LogWarning("[FireballAbility] Execute called on a non-server instance — aborting.");
                return;
            }

            Vector3 spawnPos = caster.transform.position + Vector3.up * 1.5f;
            Vector3 direction = (targetPosition - spawnPos).normalized;

            GameObject projectileGo = Object.Instantiate(ProjectilePrefab, spawnPos, Quaternion.LookRotation(direction));

            FireballProjectile projectile = projectileGo.GetComponent<FireballProjectile>();
            if (projectile != null)
            {
                projectile.Initialize(direction, ProjectileSpeed, DamageEffect);
            }
            else
            {
                Debug.LogError("[FireballAbility] ProjectilePrefab is missing FireballProjectile component!");
            }

            NetworkObject netObj = projectileGo.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                Debug.Log($"[FireballAbility] Spawned networked Fireball from {spawnPos} towards {targetPosition}.");
            }
            else
            {
                Debug.LogError("[FireballAbility] ProjectilePrefab is missing NetworkObject component! Destroying local-only instance.");
                Object.Destroy(projectileGo);
            }
        }
    }
}
