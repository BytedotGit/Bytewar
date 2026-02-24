using UnityEngine;
using Unity.Netcode;
using ByteWar.Abilities;
using ByteWar.Core;
using System;

namespace ByteWar.Survival
{
    /// <summary>
    /// Server-authoritative enemy AI.  Pursues the nearest player, attacks on cooldown,
    /// and dies when its AttributeSet Health reaches zero.
    /// Uses CharacterController for physics-based movement so buildings block pathing.
    /// </summary>
    [RequireComponent(typeof(AttributeSet))]
    public class EnemyAI : NetworkBehaviour, IDamageable, ICombatTarget
    {
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _attackRange = 2f;
        [SerializeField] private float _attackDamage = 10f;
        [SerializeField] private float _attackCooldown = 1.5f;

        private Transform _targetPlayer;
        private AttributeSet _attributes;
        private CharacterController _characterController;
        private float _lastAttackTime;
        private float _lastLogTime;
        private bool _isDead;
        private float _verticalVelocity;

        // ── Interface implementations ─────────────────────────────────────────
        /// <inheritdoc/>
        public float CurrentHealth => _attributes != null ? _attributes.Health.Value : 0f;
        /// <inheritdoc/>
        public bool IsAlive => !_isDead;
        /// <inheritdoc/>
        AttributeSet ICombatTarget.Attributes => _attributes;

        /// <summary>
        /// Raised on the server when this enemy dies (before despawn).
        /// Subscribers (e.g. EnemySpawner) use this to decrement their counter.
        /// </summary>
        public static event Action<EnemyAI> OnEnemyDied;

        // ── NGO lifecycle ─────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            _attributes = GetComponent<AttributeSet>();
            _characterController = GetComponent<CharacterController>();

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

            // Also raise on centralized event bus
            GameEventBus.EnemyDied.Raise(new EnemyDiedEvent
            {
                NetworkObjectId = NetworkObjectId,
                Position = transform.position,
            });

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
                _targetPlayer = PlayerRegistry.GetNearest(transform.position);

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
            direction.y = 0f; // Keep movement horizontal

            // Apply gravity
            if (_characterController != null && _characterController.isGrounded)
            {
                _verticalVelocity = -0.5f; // Small downward force to keep grounded
            }
            else
            {
                _verticalVelocity += -18f * Time.deltaTime; // Gravity
            }

            Vector3 move = direction * _moveSpeed * Time.deltaTime;
            move.y = _verticalVelocity * Time.deltaTime;

            if (_characterController != null)
            {
                // CharacterController.Move respects colliders — buildings will block movement
                _characterController.Move(move);
            }
            else
            {
                // Fallback for enemies without CharacterController (e.g. tests)
                Vector3 newPos = transform.position + move;
                if (Terrain.activeTerrain != null)
                {
                    float terrainY = Terrain.activeTerrain.SampleHeight(newPos)
                                   + Terrain.activeTerrain.transform.position.y + 0.5f;
                    newPos.y = terrainY;
                }
                transform.position = newPos;
            }

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
                // Line-of-sight check: don't attack through walls/buildings
                Vector3 eyePos = transform.position + Vector3.up * 1.5f;
                Vector3 targetPos = _targetPlayer.position + Vector3.up * 1.0f;
                Vector3 toTarget = targetPos - eyePos;
                float dist = toTarget.magnitude;

                if (Physics.Raycast(eyePos, toTarget.normalized, out RaycastHit losHit, dist))
                {
                    // If the ray hit something other than the target player, LOS is blocked
                    if (!losHit.transform.IsChildOf(_targetPlayer) && losHit.transform != _targetPlayer)
                    {
                        if (Time.time - _lastLogTime > 3f)
                        {
                            Debug.Log($"[EnemyAI] '{gameObject.name}' attack blocked by '{losHit.collider.name}' — no line of sight to '{_targetPlayer.name}'.");
                            _lastLogTime = Time.time;
                        }
                        return;
                    }
                }

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