using System;
using UnityEngine;

namespace ByteWar.Core
{
    /// <summary>
    /// The possible high-level game states.
    /// </summary>
    public enum GameStateType
    {
        Initializing,
        MainMenu,
        Connecting,
        Loading,
        Playing,
        Paused,
        Disconnected,
    }

    /// <summary>
    /// Lightweight state machine for high-level game flow.
    /// Hosted by <see cref="GameManager"/>. Each state is an <see cref="IGameState"/> callback.
    /// </summary>
    public class GameStateMachine
    {
        private GameStateType _currentStateType = GameStateType.Initializing;
        private IGameState _currentState;

        /// <summary>The current state type.</summary>
        public GameStateType CurrentStateType => _currentStateType;

        /// <summary>Raised when the state changes. Args: (previous, next).</summary>
        public event Action<GameStateType, GameStateType> OnStateChanged;

        /// <summary>
        /// Transitions to a new state. Calls Exit on the current state and Enter on the new one.
        /// </summary>
        public void TransitionTo(GameStateType newState, IGameState stateHandler = null)
        {
            if (newState == _currentStateType)
            {
                Debug.LogWarning($"[GameStateMachine] Already in state {newState}, ignoring transition.");
                return;
            }

            GameStateType previous = _currentStateType;
            Debug.Log($"[GameStateMachine] {previous} → {newState}");

            _currentState?.Exit();
            _currentStateType = newState;
            _currentState = stateHandler;
            _currentState?.Enter();

            OnStateChanged?.Invoke(previous, newState);
        }

        /// <summary>Tick the current state (call from Update).</summary>
        public void Tick()
        {
            _currentState?.Tick();
        }
    }
}
