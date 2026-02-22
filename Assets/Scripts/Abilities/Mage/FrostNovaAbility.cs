using UnityEngine;

namespace SurvivalRPG.Abilities.Mage
{
    [CreateAssetMenu(fileName = "FrostNova", menuName = "SurvivalRPG/Abilities/Mage/FrostNova")]
    public class FrostNovaAbility : Ability
    {
        public float Radius = 5f;
        public GameplayEffect RootEffect;
        public GameplayEffect DamageEffect;

        public override void Execute(AbilitySystemComponent caster, Vector3 targetPosition)
        {
            // Find all targets within radius
            Collider[] hitColliders = Physics.OverlapSphere(caster.transform.position, Radius);
            foreach (var hitCollider in hitColliders)
            {
                AttributeSet targetAttributes = hitCollider.GetComponent<AttributeSet>();
                if (targetAttributes != null && targetAttributes != caster.Attributes)
                {
                    if (DamageEffect != null) DamageEffect.ApplyEffect(targetAttributes);
                    // Apply RootEffect (would require a movement modifier system)
                }
            }

            Debug.Log($"Frost Nova cast at {caster.transform.position} with radius {Radius}");
        }
    }
}
