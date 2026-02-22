using UnityEngine;

namespace ByteWar.Core
{
    /// <summary>
    /// Abstraction over AbilitySystemComponent for casting abilities.
    /// </summary>
    public interface IAbilityExecutor
    {
        /// <summary>Attempt to cast the ability at the given slot index toward the target position.</summary>
        bool TryCastAbility(int abilityIndex, Vector3 targetPosition);

        /// <summary>Get remaining cooldown for the named ability.</summary>
        float GetRemainingCooldown(string abilityName);

        /// <summary>Check if the named ability is on cooldown.</summary>
        bool IsOnCooldown(string abilityName);
    }
}
