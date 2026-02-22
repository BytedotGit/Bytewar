using UnityEngine;

namespace ByteWar.Abilities.Talents
{
    [CreateAssetMenu(fileName = "AbilityModifierTalent", menuName = "ByteWar/Abilities/Talents/AbilityModifierTalent")]
    public class AbilityModifierTalent : Talent
    {
        [Header("Ability Modifiers")]
        public string TargetAbilityName;
        public float CooldownReduction;
        public float ManaCostReduction;

        public override void ApplyTalent(AbilitySystemComponent target)
        {
            base.ApplyTalent(target);

            if (!string.IsNullOrEmpty(TargetAbilityName))
            {
                if (CooldownReduction > 0)
                {
                    float current = target.GetCooldownReduction(TargetAbilityName);
                    target.SetCooldownReduction(TargetAbilityName, current + CooldownReduction);
                }

                if (ManaCostReduction > 0)
                {
                    float current = target.GetManaCostReduction(TargetAbilityName);
                    target.SetManaCostReduction(TargetAbilityName, current + ManaCostReduction);
                }
            }
        }
    }
}
