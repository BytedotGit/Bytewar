using UnityEngine;

namespace ByteWar.Abilities
{
    public enum EffectType { Damage, Healing, Buff, Debuff }
    public enum DurationType { Instant, Duration, Infinite }

    [CreateAssetMenu(fileName = "NewGameplayEffect", menuName = "ByteWar/Abilities/GameplayEffect")]
    public class GameplayEffect : ScriptableObject
    {
        [SerializeField] private string _effectName;
        [SerializeField] private EffectType _type;
        [SerializeField] private DurationType _durationType;
        [SerializeField] private float _duration;
        [SerializeField] private float _magnitude;

        public string EffectName { get => _effectName; internal set => _effectName = value; }
        public EffectType Type { get => _type; internal set => _type = value; }
        public DurationType DurationType { get => _durationType; internal set => _durationType = value; }
        public float Duration { get => _duration; internal set => _duration = value; }
        public float Magnitude { get => _magnitude; internal set => _magnitude = value; }

        public void ApplyEffect(AttributeSet target)
        {
            switch (Type)
            {
                case EffectType.Damage:
                    target.ApplyDamage(Magnitude);
                    break;
                case EffectType.Healing:
                    target.ApplyHealing(Magnitude);
                    break;
                    // Buffs and Debuffs would require a more complex modifier system
            }
        }
    }
}
