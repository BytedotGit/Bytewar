using System;
using ByteWar.Core;
using ByteWar.Survival;

namespace ByteWar.UI
{
    /// <summary>
    /// Static event bus for communicating the currently targeted enemy
    /// from <see cref="ByteWar.Core.PlayerInteraction"/> to <see cref="EnemyHealthUI"/>.
    /// Also raises <see cref="GameEventBus.EnemyTargeted"/> for centralized subscribers.
    /// </summary>
    public static class EnemyTargetTracker
    {
        /// <summary>Raised on the local client when a new enemy is targeted (or null to clear).</summary>
        public static event Action<EnemyAI> OnEnemyTargeted;

        public static void SetTarget(EnemyAI enemy)
        {
            OnEnemyTargeted?.Invoke(enemy);

            // Also raise on centralized event bus
            GameEventBus.EnemyTargeted.Raise(new EnemyTargetedEvent
            {
                Target = enemy != null ? enemy.gameObject : null,
            });
        }

        /// <summary>Removes all subscribers from <see cref="OnEnemyTargeted"/>. Use in test teardowns only.</summary>
        public static void ClearAllListeners()
        {
            OnEnemyTargeted = null;
        }
    }
}
