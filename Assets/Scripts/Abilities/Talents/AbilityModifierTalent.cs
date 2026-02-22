using UnityEngine;

namespace SurvivalRPG.Abilities.Talents
{
    [CreateAssetMenu(fileName = "AbilityModifierTalent", menuName = "SurvivalRPG/Abilities/Talents/AbilityModifierTalent")]
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
                    if (!target.CooldownReductions.ContainsKey(TargetAbilityName))
                        target.CooldownReductions[TargetAbilityName] = 0;
                    target.CooldownReductions[TargetAbilityName] += CooldownReduction;
                }

                if (ManaCostReduction > 0)
                {
                    if (!target.ManaCostReductions.ContainsKey(TargetAbilityName))
                        target.ManaCostReductions[TargetAbilityName] = 0;
                    target.ManaCostReductions[TargetAbilityName] += ManaCostReduction;
                }
            }
        }
    }
}
