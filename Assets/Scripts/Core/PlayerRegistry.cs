using System.Collections.Generic;
using UnityEngine;

namespace ByteWar.Core
{
    /// <summary>
    /// Static registry of all active player transforms.
    /// Maintained by NetworkPlayer.OnNetworkSpawn/Despawn.
    /// Replaces FindGameObjectsWithTag("Player") calls.
    /// </summary>
    public static class PlayerRegistry
    {
        private static readonly HashSet<Transform> _players = new();

        public static IReadOnlyCollection<Transform> All => _players;
        public static int Count => _players.Count;

        public static void Register(Transform player)
        {
            if (player == null) return;
            _players.Add(player);
            Debug.Log($"[PlayerRegistry] Registered player '{player.name}'. Total: {_players.Count}");
        }

        public static void Unregister(Transform player)
        {
            if (player == null) return;
            _players.Remove(player);
            Debug.Log($"[PlayerRegistry] Unregistered player '{player.name}'. Total: {_players.Count}");
        }

        /// <summary>Returns the nearest registered player to the given position, or null if none.</summary>
        public static Transform GetNearest(Vector3 position)
        {
            Transform nearest = null;
            float bestDist = float.MaxValue;

            foreach (var player in _players)
            {
                if (player == null) continue;
                float dist = Vector3.Distance(position, player.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = player;
                }
            }

            return nearest;
        }

        /// <summary>Clears the registry. Use in test teardowns and domain reloads.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            _players.Clear();
        }
    }
}
