using UnityEngine;
using Unity.Netcode;
using ByteWar.Abilities;
using ByteWar.Core;

namespace ByteWar.Survival
{
    /// <summary>
    /// Server-authoritative Fireball projectile.
    /// Moves forward each server frame; on trigger collision with an EnemyAI,
    /// applies the configured GameplayEffect damage and despawns.
    /// Requires NetworkTransform (added by PrefabGenerator) for client-side position replication.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class FireballProjectile : NetworkBehaviour
    {
        [SerializeField] private float _lifetime = 5f;

        private Vector3 _direction;
        private float _speed;
        private GameplayEffect _damageEffect;
        private bool _initialized;
        private float _spawnTime;
        private bool _isDespawning;

        /// <summary>
        /// Must be called on the server BEFORE NetworkObject.Spawn().
        /// </summary>
        public void Initialize(Vector3 direction, float speed, GameplayEffect damageEffect)
        {
            _direction = direction.normalized;
            _speed = speed;
            _damageEffect = damageEffect;
            _initialized = true;
            _spawnTime = Time.time;
            Debug.Log($"[FireballProjectile] Initialized. Direction={_direction}, Speed={_speed}");
        }

        private void Update()
        {
            if (!IsServer || !_initialized || _isDespawning) return;

            if (Time.time - _spawnTime > _lifetime)
            {
                Debug.Log("[FireballProjectile] Lifetime expired. Despawning.");
                DespawnSafe();
                return;
            }

            transform.position += _direction * _speed * Time.deltaTime;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer || _isDespawning) return;

            // Only collide with enemies that have AttributeSet
            EnemyAI enemyAI = other.GetComponent<EnemyAI>();
            AttributeSet targetAttributes = other.GetComponent<AttributeSet>();

            if (enemyAI != null && targetAttributes != null)
            {
                Debug.Log($"[FireballProjectile] Hit enemy '{other.gameObject.name}' (NetworkObjectId={enemyAI.NetworkObjectId}). Applying damage.");

                if (_damageEffect != null)
                {
                    _damageEffect.ApplyEffect(targetAttributes);
                }
                else
                {
                    targetAttributes.ApplyDamage(GameConstants.GetFireballFallbackDamage());
                    Debug.LogWarning("[FireballProjectile] No DamageEffect assigned — used flat 25 damage fallback.");
                }

                DespawnSafe();
            }
        }

        private void DespawnSafe()
        {
            if (_isDespawning) return;
            _isDespawning = true;
            if (IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
