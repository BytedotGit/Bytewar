using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ByteWar.Core
{
    /// <summary>
    /// Type-safe event channel. Subscribe with += on <see cref="OnEvent"/>.
    /// All listeners are auto-cleared on scene unload via <see cref="GameEventBus"/>.
    /// </summary>
    public sealed class GameEvent<T>
    {
        public event Action<T> OnEvent;

        public void Raise(T payload)
        {
            OnEvent?.Invoke(payload);
        }

        public void ClearListeners()
        {
            OnEvent = null;
        }
    }

    /// <summary>
    /// Centralized event bus for cross-system communication.
    /// Channels auto-cleanup on scene unload to prevent stale references.
    /// </summary>
    public static class GameEventBus
    {
        // ── Channels ──────────────────────────────────────────────────────────
        public static readonly GameEvent<EnemyDiedEvent> EnemyDied = new();
        public static readonly GameEvent<EnemyTargetedEvent> EnemyTargeted = new();
        public static readonly GameEvent<ItemEvent> ItemAdded = new();
        public static readonly GameEvent<ItemEvent> ItemRemoved = new();
        public static readonly GameEvent<BuildingPlacedEvent> BuildingPlaced = new();
        public static readonly GameEvent<GameStateChangedEvent> GameStateChanged = new();

        // Track all channels for cleanup
        private static readonly List<Action> _clearActions = new()
        {
            () => EnemyDied.ClearListeners(),
            () => EnemyTargeted.ClearListeners(),
            () => ItemAdded.ClearListeners(),
            () => ItemRemoved.ClearListeners(),
            () => BuildingPlaced.ClearListeners(),
            () => GameStateChanged.ClearListeners(),
        };

        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            if (!_initialized)
            {
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                _initialized = true;
            }
        }

        private static void OnSceneUnloaded(Scene _)
        {
            ClearAll();
            Debug.Log("[GameEventBus] All listeners cleared on scene unload.");
        }

        /// <summary>Clears all event channel listeners. Use in test teardowns.</summary>
        public static void ClearAll()
        {
            foreach (var clear in _clearActions)
                clear();
        }
    }

    // ── Event payloads ────────────────────────────────────────────────────────

    public struct EnemyDiedEvent
    {
        public ulong NetworkObjectId;
        public Vector3 Position;
    }

    public struct EnemyTargetedEvent
    {
        /// <summary>The targeted enemy's GameObject, or null to clear targeting.</summary>
        public GameObject Target;
    }

    public struct ItemEvent
    {
        public string ItemName;
        public int NewCount;
    }

    public struct BuildingPlacedEvent
    {
        public int PieceType;
        public Vector3 Position;
    }

    public struct GameStateChangedEvent
    {
        public GameStateType Previous;
        public GameStateType Current;
    }
}
