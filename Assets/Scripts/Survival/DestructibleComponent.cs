using UnityEngine;
using Unity.Netcode;
using ByteWar.Core;

namespace ByteWar.Survival
{
    [RequireComponent(typeof(NetworkObject))]
    public class DestructibleComponent : NetworkBehaviour, IDamageable
    {
        [SerializeField] private DestructibleProfile _profile;
        [SerializeField] private float _fallbackMaxHealth = 100f;
        [SerializeField] private bool _destroyOnZeroHealth = true;

        private readonly NetworkVariable<float> _health = new(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<DestructibleStateType> _state = new(DestructibleStateType.Intact, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<DamageType> _lastDamageType = new(DamageType.Blunt, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public DestructibleProfile Profile => _profile;
        public float MaxHealth => ResolveMaxHealth();
        public DestructibleStateType State => _state.Value;
        public DamageType LastDamageType => _lastDamageType.Value;

        // IDamageable contract
        public float CurrentHealth => _health.Value;
        public bool IsAlive => _health.Value > 0f;

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;

            float maxHealth = ResolveMaxHealth();
            _health.Value = maxHealth;
            _lastDamageType.Value = DamageType.Blunt;
            _state.Value = DestructibleStateEvaluator.ResolveState(_health.Value, maxHealth, _profile != null ? _profile.StateThresholds : null);

            Debug.Log($"[{nameof(DestructibleComponent)}] Spawned '{gameObject.name}' netObj={NetworkObjectId} health={_health.Value}/{maxHealth} state={_state.Value}");
        }

        public void TakeDamage(float amount)
        {
            ApplyDamage(amount, DamageType.Blunt, instigatorClientId: ulong.MaxValue);
        }

        public void ApplyDamage(float baseDamage, DamageType damageType, ulong instigatorClientId)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"[{nameof(DestructibleComponent)}] ApplyDamage called on non-server for '{gameObject.name}'. Ignoring.");
                return;
            }

            if (!IsAlive)
            {
                Debug.Log($"[{nameof(DestructibleComponent)}] '{gameObject.name}' netObj={NetworkObjectId} already destroyed. Incoming damage ignored.");
                return;
            }

            float effectiveDamage = DamageResolver.ResolveEffectiveDamage(baseDamage, damageType, _profile);
            _health.Value = Mathf.Max(0f, _health.Value - effectiveDamage);
            _lastDamageType.Value = damageType;
            _state.Value = DestructibleStateEvaluator.ResolveState(_health.Value, ResolveMaxHealth(), _profile != null ? _profile.StateThresholds : null);

            Debug.Log(
                $"[{nameof(DestructibleComponent)}] netObj={NetworkObjectId} '{gameObject.name}' instigator={instigatorClientId} " +
                $"damageType={damageType} base={baseDamage:0.##} effective={effectiveDamage:0.##} health={_health.Value:0.##}/{MaxHealth:0.##} state={_state.Value}");

            if (_health.Value <= 0f)
            {
                HandleDestroyed(instigatorClientId);
            }
        }

        internal void SetupForTest(DestructibleProfile profile, float fallbackMaxHealth = 100f, bool destroyOnZeroHealth = true)
        {
            _profile = profile;
            _fallbackMaxHealth = fallbackMaxHealth;
            _destroyOnZeroHealth = destroyOnZeroHealth;
        }

        private float ResolveMaxHealth()
        {
            if (_profile != null && _profile.MaxHealth > 0f)
                return _profile.MaxHealth;

            return Mathf.Max(1f, _fallbackMaxHealth);
        }

        private void HandleDestroyed(ulong instigatorClientId)
        {
            _state.Value = DestructibleStateType.Destroyed;
            Debug.Log($"[{nameof(DestructibleComponent)}] '{gameObject.name}' netObj={NetworkObjectId} destroyed by client {instigatorClientId}.");

            if (_destroyOnZeroHealth && NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }
    }
}