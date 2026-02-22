using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace ByteWar.Building
{
    public enum BuildingPieceType
    {
        Foundation = 0,
        Wall = 1,
    }

    /// <summary>
    /// Attached to every placed building NetworkObject.
    /// Stores the piece type, health, support flag, and snap metadata.
    /// Snap points return *target pivot positions* for neighbours (Valheim-like edge-to-edge placement).
    /// </summary>
    public class BuildingPiece : NetworkBehaviour
    {
        [Header("Identity")]
        [SerializeField] private BuildingPieceType _pieceType;
        public BuildingPieceType PieceType => _pieceType;

        [Header("Snap Metadata")]
        [Tooltip("Grid cell size for this piece (world units).")]
        [SerializeField] private float _gridSize = 4f;
        public float GridSize => _gridSize;

        [Tooltip("Height of a single wall segment (world units).")]
        [SerializeField] private float _wallHeight = 3f;
        public float WallHeight => _wallHeight;

        [Header("Health")]
        [SerializeField] private float _maxHealth = 100f;
        public float MaxHealth => _maxHealth;

        /// <summary>Current health. Server-writable.</summary>
        public NetworkVariable<float> Health = new(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Owner client ID that placed this piece (set server-side on spawn).</summary>
        public NetworkVariable<ulong> PlacedByClientId = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Whether this piece is structurally supported (ground contact or chain to ground).</summary>
        public NetworkVariable<bool> IsSupported = new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                Health.Value = _maxHealth;

            Debug.Log($"[BuildingPiece] Spawned {_pieceType} at {transform.position} by client {PlacedByClientId.Value} netObj={NetworkObjectId}");
        }

        /// <summary>
        /// Returns snap point entries: each entry is the *target pivot position* where a
        /// neighbour of the given type should be placed for edge-to-edge alignment.
        /// This is the Valheim-like approach: snap points = adjacent piece centers.
        /// </summary>
        public SnapPoint[] GetSnapPoints()
        {
            float gs = _gridSize;
            Vector3 pos = transform.position;
            var points = new List<SnapPoint>();

            if (_pieceType == BuildingPieceType.Foundation)
            {
                // Four cardinal neighbours: full grid-size offsets → edge-to-edge
                points.Add(new SnapPoint(pos + transform.forward * gs, transform.rotation, BuildingPieceType.Foundation));
                points.Add(new SnapPoint(pos - transform.forward * gs, transform.rotation, BuildingPieceType.Foundation));
                points.Add(new SnapPoint(pos + transform.right * gs, transform.rotation, BuildingPieceType.Foundation));
                points.Add(new SnapPoint(pos - transform.right * gs, transform.rotation, BuildingPieceType.Foundation));

                // Wall snap points: four edge centers, walls auto-orient perpendicular
                float halfGs = gs * 0.5f;
                points.Add(new SnapPoint(
                    pos + transform.forward * halfGs,
                    Quaternion.LookRotation(transform.forward, Vector3.up),
                    BuildingPieceType.Wall));
                points.Add(new SnapPoint(
                    pos - transform.forward * halfGs,
                    Quaternion.LookRotation(-transform.forward, Vector3.up),
                    BuildingPieceType.Wall));
                points.Add(new SnapPoint(
                    pos + transform.right * halfGs,
                    Quaternion.LookRotation(transform.right, Vector3.up),
                    BuildingPieceType.Wall));
                points.Add(new SnapPoint(
                    pos - transform.right * halfGs,
                    Quaternion.LookRotation(-transform.right, Vector3.up),
                    BuildingPieceType.Wall));
            }
            else if (_pieceType == BuildingPieceType.Wall)
            {
                // Walls snap to other walls at their left/right edges
                float halfGs = gs * 0.5f;
                points.Add(new SnapPoint(pos + transform.right * gs, transform.rotation, BuildingPieceType.Wall));
                points.Add(new SnapPoint(pos - transform.right * gs, transform.rotation, BuildingPieceType.Wall));
            }

            return points.ToArray();
        }

        /// <summary>Server: apply damage. Destroys the piece if health reaches zero.</summary>
        public void TakeDamage(float amount)
        {
            if (!IsServer) return;
            Health.Value = Mathf.Max(0f, Health.Value - amount);
            Debug.Log($"[BuildingPiece] {_pieceType} netObj={NetworkObjectId} took {amount} damage, health={Health.Value}");
            if (Health.Value <= 0f)
            {
                Debug.Log($"[BuildingPiece] {_pieceType} netObj={NetworkObjectId} destroyed.");
                NetworkObject.Despawn(true);
            }
        }

        /// <summary>Server: repair to full health.</summary>
        public void Repair()
        {
            if (!IsServer) return;
            float before = Health.Value;
            Health.Value = _maxHealth;
            Debug.Log($"[BuildingPiece] {_pieceType} netObj={NetworkObjectId} repaired {before:0} → {_maxHealth}");
        }
    }

    /// <summary>
    /// A snap point representing where a new piece should be placed.
    /// Position = target piece pivot, Rotation = suggested orientation, TargetType = which piece type this snap is for.
    /// </summary>
    public struct SnapPoint
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public BuildingPieceType TargetType;

        public SnapPoint(Vector3 position, Quaternion rotation, BuildingPieceType targetType)
        {
            Position = position;
            Rotation = rotation;
            TargetType = targetType;
        }
    }
}
