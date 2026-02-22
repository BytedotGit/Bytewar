using System;
using SurvivalRPG.Survival;

namespace SurvivalRPG.UI
{
    /// <summary>
    /// Static event bus for communicating the currently targeted enemy
    /// from <see cref="SurvivalRPG.Core.PlayerInteraction"/> to <see cref="EnemyHealthUI"/>.
    /// </summary>
    public static class EnemyTargetTracker
    {
        /// <summary>Raised on the local client when a new enemy is targeted (or null to clear).</summary>
        public static event Action<EnemyAI> OnEnemyTargeted;

        public static void SetTarget(EnemyAI enemy)
        {
            OnEnemyTargeted?.Invoke(enemy);
        }

        /// <summary>Removes all subscribers from <see cref="OnEnemyTargeted"/>. Use in test teardowns only.</summary>
        public static void ClearAllListeners()
        {
            OnEnemyTargeted = null;
        }
    }
}
