using UnityEngine;

namespace ByteWar.Abilities.Mage
{
    [CreateAssetMenu(fileName = "Blink", menuName = "ByteWar/Abilities/Mage/Blink")]
    public class BlinkAbility : Ability
    {
        public float MaxDistance = 15f;

        public override void Execute(AbilitySystemComponent caster, Vector3 targetPosition)
        {
            Vector3 direction = (targetPosition - caster.transform.position).normalized;
            Vector3 destination = caster.transform.position + direction * MaxDistance;

            // Basic collision check to prevent teleporting through walls
            if (Physics.Raycast(caster.transform.position, direction, out RaycastHit hit, MaxDistance))
            {
                destination = hit.point - direction * 0.5f; // Stop slightly before the wall
            }

            caster.transform.position = destination;
            Debug.Log($"Blink cast. Teleported to {destination}");
        }
    }
}
