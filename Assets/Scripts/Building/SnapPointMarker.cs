using UnityEngine;

namespace ByteWar.Building
{
    /// <summary>
    /// Connection type for snap points. No longer used for snap matching
    /// (Valheim-style pure nearest-pair), but retained for structural
    /// integrity visuals and future UI cues.
    /// </summary>
    public enum SnapPointType
    {
        /// <summary>Generic — connects to any other snap point.</summary>
        Generic = 0,
        /// <summary>Bottom connection — foundation tops, wall bottoms, floor undersides.</summary>
        Bottom = 1,
        /// <summary>Top connection — wall tops, pole tops, for stacking.</summary>
        Top = 2,
        /// <summary>Side/edge connection — wall sides, floor edges, horizontal connections.</summary>
        Side = 3,
        /// <summary>Ridge connection — roof peaks.</summary>
        Ridge = 4,
        /// <summary>Corner connection — structural corners where multiple pieces meet.</summary>
        Corner = 5,
    }

    /// <summary>
    /// Marker component placed on child GameObjects of building piece prefabs.
    /// The child Transform's local position IS the snap point.
    /// Valheim-style: pure nearest-pair matching — no type compatibility checks.
    /// </summary>
    public class SnapPointMarker : MonoBehaviour
    {
        [SerializeField] private SnapPointType _snapType = SnapPointType.Generic;

        /// <summary>Connection type of this snap point (for structural/UI purposes, not snap matching).</summary>
        public SnapPointType SnapType { get => _snapType; set => _snapType = value; }

        /// <summary>World position of this snap point (convenience accessor).</summary>
        public Vector3 WorldPosition => transform.position;

        /// <summary>Local position relative to the parent BuildingPiece.</summary>
        public Vector3 LocalPosition => transform.localPosition;

        /// <summary>
        /// Legacy compatibility check. No longer used for snap matching
        /// (Valheim-style uses pure nearest-pair). Retained for tests and
        /// potential future structural-integrity visuals.
        /// </summary>
        [System.Obsolete("Snap matching no longer uses type compatibility. Use pure nearest-pair instead.")]
        public static bool AreCompatible(SnapPointType a, SnapPointType b)
        {
            if (a == SnapPointType.Generic || b == SnapPointType.Generic) return true;
            if (a == SnapPointType.Top && b == SnapPointType.Bottom) return true;
            if (a == SnapPointType.Bottom && b == SnapPointType.Top) return true;
            // Side, Corner, Ridge connect to their own kind; Top/Bottom are directional
            if (a == b && a != SnapPointType.Top && a != SnapPointType.Bottom) return true;
            return false;
        }
    }
}
