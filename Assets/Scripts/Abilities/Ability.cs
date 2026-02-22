using UnityEngine;

namespace SurvivalRPG.Abilities
{
    public abstract class Ability : ScriptableObject
    {
        public string AbilityName;
        public float ManaCost;
        public float Cooldown;

        public abstract void Execute(AbilitySystemComponent caster, Vector3 targetPosition);
    }
}
