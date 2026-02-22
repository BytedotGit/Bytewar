using UnityEngine;

namespace ByteWar.Building
{
    /// <summary>
    /// Static utility for building snap logic: grid snapping and adjacency (edge-to-edge) snapping.
    /// Extracted from BuildingController for single-responsibility and testability.
    /// </summary>
    public static class BuildingSnap
    {
        /// <summary>Snap a world position to the nearest grid intersection.</summary>
        public static Vector3 SnapToGrid(Vector3 pos, float gridSize)
        {
            if (gridSize <= 0f) return pos;
            return new Vector3(
                Mathf.Round(pos.x / gridSize) * gridSize,
                pos.y,
                Mathf.Round(pos.z / gridSize) * gridSize
            );
        }

        /// <summary>
        /// Valheim-like edge-to-edge snapping. Finds the nearest SnapPoint from
        /// existing BuildingPieces that matches the new piece type. Updates position
        /// AND rotation (walls auto-orient to face outward from foundation edges).
        /// </summary>
        /// <returns>True if a valid snap point was found.</returns>
        public static bool TryAdjacencySnap(
            ref Vector3 pos,
            ref Quaternion rotation,
            BuildingPieceType newType,
            float adjacencySnapRadius,
            float gridSize,
            Collider[] overlapBuffer)
        {
            int count = Physics.OverlapSphereNonAlloc(pos, adjacencySnapRadius + gridSize, overlapBuffer);

            float bestDist = adjacencySnapRadius;
            SnapPoint bestSnap = default;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                var col = overlapBuffer[i];
                if (col == null) continue;

                var piece = col.GetComponentInParent<BuildingPiece>();
                if (piece == null || !piece.IsSpawned) continue;

                SnapPoint[] snapPoints = piece.GetSnapPoints();
                foreach (var sp in snapPoints)
                {
                    if (sp.TargetType != newType) continue;

                    float dist = Vector3.Distance(pos, sp.Position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestSnap = sp;
                        found = true;
                    }
                }
            }

            if (found)
            {
                pos = bestSnap.Position;
                rotation = bestSnap.Rotation;
            }

            return found;
        }

        /// <summary>
        /// Basic stability check: Foundations place freely. Walls must be snapped to a foundation edge.
        /// </summary>
        public static bool CheckSupport(
            Vector3 pos,
            BuildingPieceType type,
            bool isSnapped,
            float gridSize,
            Collider[] overlapBuffer)
        {
            if (type == BuildingPieceType.Foundation) return true;
            if (!isSnapped) return false;

            int count = Physics.OverlapSphereNonAlloc(pos, gridSize * 0.75f, overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                var col = overlapBuffer[i];
                if (col == null) continue;

                var piece = col.GetComponentInParent<BuildingPiece>();
                if (piece != null && piece.IsSpawned && piece.PieceType == BuildingPieceType.Foundation)
                    return true;
            }

            return false;
        }
    }
}
