namespace ByteWar.Core
{
    /// <summary>
    /// A discrete state in the game state machine.
    /// </summary>
    public interface IGameState
    {
        /// <summary>Called when entering this state.</summary>
        void Enter();

        /// <summary>Called when exiting this state.</summary>
        void Exit();

        /// <summary>Called every frame while this state is active.</summary>
        void Tick();
    }
}
