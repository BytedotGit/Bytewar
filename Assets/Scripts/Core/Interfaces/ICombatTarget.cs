using ByteWar.Abilities;

namespace ByteWar.Core
{
    /// <summary>
    /// A damageable entity with combat attributes. Implemented by EnemyAI and (potentially) players.
    /// </summary>
    public interface ICombatTarget
    {
        /// <summary>The entity's combat attributes (health, mana, etc.).</summary>
        AttributeSet Attributes { get; }

        /// <summary>Whether this combat target is still alive.</summary>
        bool IsAlive { get; }
    }
}
