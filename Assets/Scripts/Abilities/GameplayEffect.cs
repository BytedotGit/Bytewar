using UnityEngine;

namespace ByteWar.Abilities
{
    public enum EffectType { Damage, Healing, Buff, Debuff }
    public enum DurationType { Instant, Duration, Infinite }

    [CreateAssetMenu(fileName = "NewGameplayEffect", menuName = "ByteWar/Abilities/GameplayEffect")]
    public class GameplayEffect : ScriptableObject
    {
        public string EffectName;
        public EffectType Type;
        public DurationType DurationType;
        public float Duration;
        public float Magnitude;

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
