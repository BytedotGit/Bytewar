using UnityEngine;
using Unity.Netcode;
using SurvivalRPG.Abilities;
using System;

namespace SurvivalRPG.Survival
{
    /// <summary>
    /// Server-authoritative enemy AI.  Pursues the nearest player, attacks on cooldown,
    /// and dies when its AttributeSet Health reaches zero.
    /// </summary>
    [RequireComponent(typeof(AttributeSet))]
    public class EnemyAI : NetworkBehaviour
    {
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _attackRange = 2f;
        [SerializeField] private float _attackDamage = 10f;
        [SerializeField] private float _attackCooldown = 1.5f;

        private Transform _targetPlayer;
        private AttributeSet _attributes;
        private float _lastAttackTime;
        private float _lastLogTime;
        private bool _isDead;

        /// <summary>
        /// Raised on the server when this enemy dies (before despawn).
        /// Subscribers (e.g. EnemySpawner) use this to decrement their counter.
        /// </summary>
        public static event Action<EnemyAI> OnEnemyDied;

        // ── NGO lifecycle ─────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            _attributes = GetComponent<AttributeSet>();

            if (IsServer)
            {
                if (Terrain.activeTerrain != null)
                {
                    float y = Terrain.activeTerrain.SampleHeight(transform.position)
                            + Terrain.activeTerrain.transform.position.y + 0.5f;
                    transform.position = new Vector3(transform.position.x, y, transform.position.z);
                }

                Debug.Log($"[EnemyAI] Spawned '{gameObject.name}' (NetworkObjectId={NetworkObjectId}) at {transform.position}.");

                if (_attributes != null)
                {
                    _attributes.Health.OnValueChanged += OnHealthChanged;
                }
                else
                {
                    Debug.LogError($"[EnemyAI] '{gameObject.name}' is missing an AttributeSet component!");
                }

                StartCoroutine(AILoop());
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_attributes != null)
            {
                _attributes.Health.OnValueChanged -= OnHealthChanged;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Applies damage to this enemy.  Must be called on the server.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"[EnemyAI] TakeDamage called on client for '{gameObject.name}'. Ignoring.");
                return;
            }

            if (_attributes == null) _attributes = GetComponent<AttributeSet>();

            if (_attributes != null)
            {
                _attributes.ApplyDamage(amount);
                Debug.Log($"[EnemyAI] '{gameObject.name}' took {amount} damage. Health={_attributes.Health.Value}");
            }
            else
            {
                Debug.LogWarning($"[EnemyAI] '{gameObject.name}' has no AttributeSet — cannot apply damage.");
            }
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private void OnHealthChanged(float previous, float current)
        {
            if (!IsServer || _isDead) return;

            Debug.Log($"[EnemyAI] '{gameObject.name}' health: {previous:F1} → {current:F1}");

            if (current <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            Debug.Log($"[EnemyAI] '{gameObject.name}' (NetworkObjectId={NetworkObjectId}) died!");
            OnEnemyDied?.Invoke(this);

            if (IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        private System.Collections.IEnumerator AILoop()
        {
            while (true)
            {
                if (IsServer && !_isDead)
                {
                    FindTarget();

                    if (_targetPlayer != null)
                    {
                        float distance = Vector3.Distance(transform.position, _targetPlayer.position);

                        if (distance > _attackRange)
                        {
                            MoveTowardsTarget();
                        }
                        else
                        {
                            AttackTarget();
                        }
                    }
                }
                yield return null;
            }
        }

        private void FindTarget()
        {
            // Clear destroyed target
            if (_targetPlayer != null && _targetPlayer == null)
            {
                _targetPlayer = null;
            }

            if (_targetPlayer == null)
            {
                GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
                float closestDistance = float.MaxValue;

                foreach (var player in players)
                {
                    float distance = Vector3.Distance(transform.position, player.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        _targetPlayer = player.transform;
                    }
                }

                if (_targetPlayer != null)
                {
                    Debug.Log($"[EnemyAI] '{gameObject.name}' found target: {_targetPlayer.name}");
                }
            }
        }

        private void MoveTowardsTarget()
        {
            if (_targetPlayer == null) return;

            if (Time.time - _lastLogTime > 5f)
            {
                Debug.Log($"[EnemyAI] '{gameObject.name}' moving towards {_targetPlayer.name}");
                _lastLogTime = Time.time;
            }

            Vector3 direction = (_targetPlayer.position - transform.position).normalized;
            Vector3 newPos = transform.position + direction * _moveSpeed * Time.deltaTime;

            // Snap to terrain surface each step so the enemy doesn't float or sink
            if (Terrain.activeTerrain != null)
            {
                float terrainY = Terrain.activeTerrain.SampleHeight(newPos)
                               + Terrain.activeTerrain.transform.position.y + 0.5f;
                newPos.y = terrainY;
            }

            transform.position = newPos;

            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        private void AttackTarget()
        {
            if (_targetPlayer == null) return;

            if (Time.time - _lastAttackTime >= _attackCooldown)
            {
                Debug.Log($"[EnemyAI] '{gameObject.name}' attacking '{_targetPlayer.name}' for {_attackDamage} damage.");

                AttributeSet targetAttributes = _targetPlayer.GetComponent<AttributeSet>();
                if (targetAttributes != null)
                {
                    targetAttributes.ApplyDamage(_attackDamage);
                    Debug.Log($"[EnemyAI] '{gameObject.name}' successfully dealt {_attackDamage} damage to '{_targetPlayer.name}'.");
                }
                else
                {
                    Debug.LogWarning($"[EnemyAI] Target '{_targetPlayer.name}' has no AttributeSet — clearing target.");
                    _targetPlayer = null;
                }

                _lastAttackTime = Time.time;
            }
        }
    }
}