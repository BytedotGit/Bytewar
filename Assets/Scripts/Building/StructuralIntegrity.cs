using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace ByteWar.Building
{
    /// <summary>
    /// Valheim-style structural integrity system. Calculates support values based on
    /// ground contact and neighbor propagation with material-dependent falloff.
    /// Pieces without sufficient support take structural damage over time and collapse.
    /// </summary>
    public class StructuralIntegrity : MonoBehaviour
    {
        // ── Configuration ─────────────────────────────────────────────────────

        [SerializeField] private float _supportCheckInterval = 2f;
        [SerializeField] private float _supportSearchRadius = 5f;
        [SerializeField] private float _unsupportedDamageRate = 10f;

        // ── Support propagation constants (Valheim-inspired) ─────────────────

        /// <summary>Horizontal support falloff per neighbor step, by material.</summary>
        private static readonly Dictionary<StructuralMaterial, float> HorizontalFalloff = new()
        {
            { StructuralMaterial.Wood, 0.125f },
            { StructuralMaterial.Stone, 0.10f },
            { StructuralMaterial.Iron, 0.0666f },
            { StructuralMaterial.HardWood, 0.10f },
        };

        /// <summary>Vertical support falloff per neighbor step, by material.</summary>
        private static readonly Dictionary<StructuralMaterial, float> VerticalFalloff = new()
        {
            { StructuralMaterial.Wood, 0.10f },
            { StructuralMaterial.Stone, 0.08f },
            { StructuralMaterial.Iron, 0.05f },
            { StructuralMaterial.HardWood, 0.08f },
        };

        /// <summary>Min support value before the piece takes structural damage.</summary>
        private const float MinSupportThreshold = 0.05f;

        /// <summary>Support value for ground-touching pieces.</summary>
        private const float GroundSupportValue = 1.0f;

        /// <summary>Raycast distance to detect ground beneath a piece.</summary>
        private const float GroundCheckDistance = 1.0f;

        // ── Runtime state ─────────────────────────────────────────────────────

        private BuildingPiece _piece;
        private float _supportValue = 1.0f;
        private float _nextCheckTime;
        private readonly Collider[] _neighborBuffer = new Collider[32];

        /// <summary>Current structural support value (0.0 = unsupported, 1.0 = full).</summary>
        public float SupportValue => _supportValue;

        /// <summary>Whether this piece is touching the ground.</summary>
        public bool IsGrounded { get; private set; }

        // ── Static registry for efficient neighbor lookup ─────────────────────

        private static readonly List<StructuralIntegrity> AllInstances = new();

        /// <summary>All active structural integrity instances (for neighbor scanning).</summary>
        public static IReadOnlyList<StructuralIntegrity> Instances => AllInstances;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _piece = GetComponent<BuildingPiece>();
        }

        private void OnEnable()
        {
            AllInstances.Add(this);
        }

        private void OnDisable()
        {
            AllInstances.Remove(this);
        }

        private void Start()
        {
            // Stagger check times to distribute load across frames
            _nextCheckTime = Time.time + Random.Range(0f, _supportCheckInterval);
            RecalculateSupport();
        }

        private void Update()
        {
            if (Time.time < _nextCheckTime) return;
            _nextCheckTime = Time.time + _supportCheckInterval;

            RecalculateSupport();
            ApplyStructuralDamage();
        }

        // ── Support calculation ───────────────────────────────────────────────

        /// <summary>
        /// Recalculates structural support based on ground contact and neighbors.
        /// Ground-touching pieces get full support. Others inherit from neighbors with falloff.
        /// </summary>
        public void RecalculateSupport()
        {
            if (_piece == null) return;

            // Check ground contact
            IsGrounded = CheckGroundContact();
            if (IsGrounded && _piece.PieceType == BuildingPieceType.Foundation)
            {
                _supportValue = GroundSupportValue;
                UpdatePieceSupport();
                return;
            }

            // Scan neighbors and inherit best support with falloff
            float bestSupport = IsGrounded ? GroundSupportValue : 0f;
            StructuralMaterial material = _piece.Material;

            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _supportSearchRadius, _neighborBuffer);

            for (int i = 0; i < count; i++)
            {
                if (_neighborBuffer[i] == null) continue;
                var neighbor = _neighborBuffer[i].GetComponentInParent<StructuralIntegrity>();
                if (neighbor == null || neighbor == this) continue;

                float delta = CalculateFalloff(transform.position, neighbor.transform.position, material);
                float inherited = neighbor._supportValue - delta;
                if (inherited > bestSupport)
                    bestSupport = inherited;
            }

            _supportValue = Mathf.Clamp01(bestSupport);
            UpdatePieceSupport();
        }

        /// <summary>
        /// Calculates the support falloff between two positions based on material type.
        /// Horizontal distance costs more than vertical for most materials.
        /// </summary>
        private static float CalculateFalloff(Vector3 from, Vector3 to, StructuralMaterial material)
        {
            float horizontalDist = Vector3.Distance(
                new Vector3(from.x, 0f, from.z),
                new Vector3(to.x, 0f, to.z));
            float verticalDist = Mathf.Abs(from.y - to.y);

            float hFalloff = HorizontalFalloff.GetValueOrDefault(material, 0.125f);
            float vFalloff = VerticalFalloff.GetValueOrDefault(material, 0.10f);

            // Normalize by grid size (4 units) so falloff is per-piece-step
            return (horizontalDist / 4f) * hFalloff + (verticalDist / 3f) * vFalloff;
        }

        private bool CheckGroundContact()
        {
            // Raycast downward from piece center to detect ground
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            return Physics.Raycast(origin, Vector3.down, GroundCheckDistance + 0.1f);
        }

        private void UpdatePieceSupport()
        {
            if (_piece == null) return;
            bool supported = _supportValue >= MinSupportThreshold;

            // Update the NetworkVariable if we have server authority
            var netBehaviour = _piece as NetworkBehaviour;
            if (netBehaviour != null && netBehaviour.IsServer)
            {
                _piece.IsSupported.Value = supported;
            }
        }

        // ── Structural damage ─────────────────────────────────────────────────

        private void ApplyStructuralDamage()
        {
            if (_supportValue >= MinSupportThreshold) return;

            var netBehaviour = _piece as NetworkBehaviour;
            if (netBehaviour == null || !netBehaviour.IsServer) return;

            float damage = _unsupportedDamageRate * _supportCheckInterval;
            _piece.TakeDamage(damage);
            Debug.Log($"[StructuralIntegrity] {_piece.PieceType} netObj={_piece.NetworkObjectId} " +
                      $"taking structural damage ({damage:0.0}), support={_supportValue:0.00}");
        }

        // ── Debug visualization ───────────────────────────────────────────────

        /// <summary>Returns a color representing the support level for debug visualization.</summary>
        public Color GetSupportColor()
        {
            if (_supportValue >= 0.8f) return Color.blue;     // Excellent
            if (_supportValue >= 0.5f) return Color.green;    // Good
            if (_supportValue >= 0.2f) return Color.yellow;   // Warning
            return Color.red;                                  // Critical
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = GetSupportColor();
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"Support: {_supportValue:0.00}");
        }
#endif
    }
}
