using UnityEngine;
using Unity.Netcode;
using ByteWar.Core;

namespace ByteWar.Building
{
    public enum BuildingPieceType
    {
        Foundation = 0,
        Wall = 1,
        Floor = 2,
        Ramp = 3,
        Roof26 = 4,
        Stairs = 5,
        Pole = 6,
        Beam = 7,
        AngledWall = 8,
        DoorFrame = 9,
        Window = 10,
        HalfWall = 11,
    }

    /// <summary>
    /// Attached to every placed building NetworkObject.
    /// Stores the piece type, health, and support flag.
    /// Snap points are defined by <see cref="SnapPointMarker"/> children on the prefab.
    /// </summary>
    public class BuildingPiece : NetworkBehaviour, IDamageable, IPersistable
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

        [Header("Structural")]
        [SerializeField] private StructuralMaterial _material = StructuralMaterial.Wood;
        /// <summary>Structural material type for support propagation falloff.</summary>
        public StructuralMaterial Material => _material;

        [Header("Comfort")]
        [SerializeField] private float _comfortValue;
        [SerializeField] private ComfortGroup _comfortGroup = ComfortGroup.None;
        /// <summary>Comfort contribution of this piece when placed in a shelter.</summary>
        public float ComfortValue => _comfortValue;
        /// <summary>Comfort group for stacking rules (only one per group counts).</summary>
        public ComfortGroup ComfortGroup => _comfortGroup;

        [Header("Placement Restrictions")]
        [SerializeField] private bool _groundPiece;
        [SerializeField] private bool _noInWater;
        [SerializeField] private bool _notOnFloor;
        [SerializeField] private bool _notOnWall;
        [SerializeField] private bool _onlyOnFlat;
        /// <summary>Requires terrain/ground beneath for placement.</summary>
        public bool GroundPiece => _groundPiece;
        /// <summary>Cannot be placed in water.</summary>
        public bool NoInWater => _noInWater;
        /// <summary>Cannot be placed on a floor piece.</summary>
        public bool NotOnFloor => _notOnFloor;
        /// <summary>Cannot be placed on a wall piece.</summary>
        public bool NotOnWall => _notOnWall;
        /// <summary>Requires flat surface for placement.</summary>
        public bool OnlyOnFlat => _onlyOnFlat;

        [Header("Health")]
        [SerializeField] private float _maxHealth = 100f;
        public float MaxHealth => _maxHealth;

        /// <summary>Current health. Server-writable.</summary>
        public NetworkVariable<float> Health = new(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ── IDamageable ───────────────────────────────────────────────────────────
        /// <inheritdoc/>
        public float CurrentHealth => Health.Value;
        /// <inheritdoc/>
        public bool IsAlive => Health.Value > 0f;

        // ── IPersistable ──────────────────────────────────────────────────────────
        /// <inheritdoc/>
        public string Serialize()
        {
            Vector3 euler = transform.eulerAngles;
            var entry = new BuildingSaveEntry
            {
                PieceType = (int)_pieceType,
                PosX = transform.position.x,
                PosY = transform.position.y,
                PosZ = transform.position.z,
                RotX = euler.x,
                RotY = euler.y,
                RotZ = euler.z,
                PlacedByClientId = PlacedByClientId.Value,
            };
            return UnityEngine.JsonUtility.ToJson(entry);
        }
        /// <inheritdoc/>
        public void Deserialize(string data)
        {
            var entry = UnityEngine.JsonUtility.FromJson<BuildingSaveEntry>(data);
            transform.position = new UnityEngine.Vector3(entry.PosX, entry.PosY, entry.PosZ);
            transform.rotation = UnityEngine.Quaternion.Euler(entry.RotX, entry.RotY, entry.RotZ);
        }

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
}
