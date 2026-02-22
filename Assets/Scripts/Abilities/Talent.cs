using UnityEngine;

namespace ByteWar.Abilities
{
    [CreateAssetMenu(fileName = "NewTalent", menuName = "ByteWar/Abilities/Talent")]
    public class Talent : ScriptableObject
    {
        public string TalentName;
        public string Description;
        public int MaxRank = 1;

        // Example modifiers
        public float HealthBonus;
        public float ManaBonus;

        public virtual void ApplyTalent(AbilitySystemComponent target)
        {
            if (HealthBonus > 0)
            {
                target.Attributes.MaxHealth.Value += HealthBonus;
                target.Attributes.Health.Value += HealthBonus;
            }
            if (ManaBonus > 0)
            {
                target.Attributes.MaxMana.Value += ManaBonus;
                target.Attributes.Mana.Value += ManaBonus;
            }
        }
    }
}
