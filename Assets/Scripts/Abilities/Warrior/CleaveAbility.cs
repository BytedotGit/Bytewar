using UnityEngine;
using ByteWar.Core;

namespace ByteWar.Abilities.Warrior
{
    /// <summary>
    /// Iron Tide Cleave — a warrior AoE melee ability.
    /// Damages all <see cref="IDamageable"/> entities in a forward arc.
    /// Server-authoritative execution. Uses <see cref="GameplayEffect"/> for damage.
    /// </summary>
    [CreateAssetMenu(fileName = "CleaveAbility", menuName = "ByteWar/Abilities/Warrior/Cleave")]
    public class CleaveAbility : Ability
    {
        [Header("Cleave Settings")]
        [SerializeField] private float _radius = 4f;
        [SerializeField] private float _arcAngle = 120f;
        [SerializeField] private GameplayEffect _damageEffect;

        public float Radius { get => _radius; internal set => _radius = value; }
        public float ArcAngle { get => _arcAngle; internal set => _arcAngle = value; }
        public GameplayEffect DamageEffect { get => _damageEffect; internal set => _damageEffect = value; }

        private static readonly Collider[] HitBuffer = new Collider[32];

        public override void Execute(AbilitySystemComponent caster, Vector3 targetPosition)
        {
            if (!caster.IsServer)
            {
                Debug.LogWarning("[CleaveAbility] Execute called on non-server — aborting.");
                return;
            }

            Vector3 origin = caster.transform.position + Vector3.up * 1f;
            Vector3 forward = caster.transform.forward;

            int hitCount = Physics.OverlapSphereNonAlloc(origin, _radius, HitBuffer);
            int damaged = 0;

            Debug.Log($"[CleaveAbility] Executing cleave: origin={origin} forward={forward} radius={_radius} arc={_arcAngle} overlaps={hitCount}");

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = HitBuffer[i];
                if (col == null) continue;

                // Don't damage self
                if (col.transform.IsChildOf(caster.transform)) continue;

                // Arc check: target must be within the forward cone
                Vector3 toTarget = (col.transform.position - origin).normalized;
                float angle = Vector3.Angle(forward, toTarget);
                if (angle > _arcAngle * 0.5f) continue;

                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                // Apply damage effect
                if (_damageEffect != null)
                {
                    var attributes = col.GetComponentInParent<Abilities.AttributeSet>();
                    if (attributes != null)
                    {
                        _damageEffect.ApplyEffect(attributes);
                        damaged++;
                        Debug.Log($"[CleaveAbility] Hit '{col.gameObject.name}' for {_damageEffect.Magnitude} damage. angle={angle:0.0}");
                    }
                }
                else
                {
                    // Fallback: direct damage if no effect assigned
                    var attributes = col.GetComponentInParent<Abilities.AttributeSet>();
                    if (attributes != null)
                    {
                        attributes.ApplyDamage(GameConstants.GetMeleeDamage());
                        damaged++;
                        Debug.Log($"[CleaveAbility] Hit '{col.gameObject.name}' with fallback damage={GameConstants.GetMeleeDamage()}. angle={angle:0.0}");
                    }
                }
            }

            Debug.Log($"[CleaveAbility] Cleave complete: hit {damaged} targets.");
        }
    }
}
