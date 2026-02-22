namespace ByteWar.Core
{
    /// <summary>
    /// Any entity that can receive damage. Implemented by EnemyAI, ResourceNode, BuildingPiece.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>Current health value.</summary>
        float CurrentHealth { get; }

        /// <summary>Whether this entity is still alive (health > 0).</summary>
        bool IsAlive { get; }

        /// <summary>
        /// Apply damage to this entity. Must be called on the server.
        /// </summary>
        void TakeDamage(float amount);
    }
}
