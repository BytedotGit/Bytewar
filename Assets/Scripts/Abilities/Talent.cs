using UnityEngine;

namespace ByteWar.Abilities
{
    [CreateAssetMenu(fileName = "NewTalent", menuName = "ByteWar/Abilities/Talent")]
    public class Talent : ScriptableObject
    {
        [SerializeField] private string _talentName;
        [SerializeField] private string _description;
        [SerializeField] private int _maxRank = 1;

        [SerializeField] private float _healthBonus;
        [SerializeField] private float _manaBonus;

        public string TalentName { get => _talentName; internal set => _talentName = value; }
        public string Description { get => _description; internal set => _description = value; }
        public int MaxRank { get => _maxRank; internal set => _maxRank = value; }
        public float HealthBonus { get => _healthBonus; internal set => _healthBonus = value; }
        public float ManaBonus { get => _manaBonus; internal set => _manaBonus = value; }

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
