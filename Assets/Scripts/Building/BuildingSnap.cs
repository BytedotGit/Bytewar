using UnityEngine;
using System.Collections.Generic;

namespace ByteWar.Building
{
    /// <summary>
    /// Static utility for building snap logic: grid snapping and Valheim-style
    /// pure nearest-pair adjacency snapping via <see cref="SnapPointMarker"/> children.
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
        /// Valheim-style pure nearest-pair snap. Finds the globally closest
        /// snap-point pair between the preview piece and all placed pieces,
        /// with no type filtering or priority tiers.
        /// The cursor position naturally selects the correct snap pair.
        /// Rotation stays player-controlled.
        /// </summary>
        /// <param name="pos">Cursor world position (updated to snapped position on success).</param>
        /// <param name="rotation">Player-chosen rotation (unchanged by snap).</param>
        /// <param name="previewSnapLocals">Local-space snap point positions of the preview piece.</param>
        /// <param name="adjacencySnapRadius">Max distance between any two snap points to match.</param>
        /// <param name="overlapBuffer">Pre-allocated collider buffer for OverlapSphere.</param>
        /// <returns>True if a valid snap pair was found.</returns>
        public static bool TryAdjacencySnap(
            ref Vector3 pos,
            Quaternion rotation,
            List<Vector3> previewSnapLocals,
            float adjacencySnapRadius,
            Collider[] overlapBuffer)
        {
            return TryAdjacencySnap(
                ref pos,
                rotation,
                previewSnapLocals,
                null,
                adjacencySnapRadius,
                overlapBuffer,
                null);
        }

        /// <summary>
        /// Valheim-style pure nearest-pair snap with two practical refinements:
        /// - If <paramref name="preferredPiece"/> is provided (the piece the player is aiming at), try it first.
        /// - SnapPointType compatibility is largely type-agnostic, but Ridge points only snap to Ridge points.
        ///   This prevents roofs from snapping a ridge point onto a wall/floor.
        /// </summary>
        public static bool TryAdjacencySnap(
            ref Vector3 pos,
            Quaternion rotation,
            IReadOnlyList<Vector3> previewSnapLocals,
            IReadOnlyList<SnapPointType> previewSnapTypes,
            float adjacencySnapRadius,
            Collider[] overlapBuffer,
            BuildingPiece preferredPiece)
        {
            if (previewSnapLocals == null || previewSnapLocals.Count == 0) return false;

            if (preferredPiece != null)
            {
                if (TryAdjacencySnapToPiece(ref pos, rotation, previewSnapLocals, previewSnapTypes, adjacencySnapRadius, preferredPiece))
                    return true;
            }

            int count = Physics.OverlapSphereNonAlloc(pos, adjacencySnapRadius + 6f, overlapBuffer);

            float bestDist = adjacencySnapRadius;
            Vector3 bestPos = pos;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                var col = overlapBuffer[i];
                if (col == null) continue;

                var piece = col.GetComponentInParent<BuildingPiece>();
                if (piece == null) continue;

                if (TryAdjacencySnapToPiece(ref pos, rotation, previewSnapLocals, previewSnapTypes, adjacencySnapRadius, piece,
                        ref bestDist, ref bestPos))
                {
                    found = true;
                }
            }

            if (found)
            {
                pos = bestPos;
                return true;
            }

            return false;
        }

        private static bool TryAdjacencySnapToPiece(
            ref Vector3 pos,
            Quaternion rotation,
            IReadOnlyList<Vector3> previewSnapLocals,
            IReadOnlyList<SnapPointType> previewSnapTypes,
            float adjacencySnapRadius,
            BuildingPiece piece)
        {
            float bestDist = adjacencySnapRadius;
            Vector3 bestPos = pos;
            bool found = TryAdjacencySnapToPiece(ref pos, rotation, previewSnapLocals, previewSnapTypes, adjacencySnapRadius, piece,
                ref bestDist, ref bestPos);
            if (found)
                pos = bestPos;
            return found;
        }

        private static bool TryAdjacencySnapToPiece(
            ref Vector3 pos,
            Quaternion rotation,
            IReadOnlyList<Vector3> previewSnapLocals,
            IReadOnlyList<SnapPointType> previewSnapTypes,
            float adjacencySnapRadius,
            BuildingPiece piece,
            ref float bestDist,
            ref Vector3 bestPos)
        {
            var placedMarkers = piece.GetComponentsInChildren<SnapPointMarker>();
            if (placedMarkers == null || placedMarkers.Length == 0) return false;

            bool improved = false;
            for (int p = 0; p < previewSnapLocals.Count; p++)
            {
                Vector3 previewLocal = previewSnapLocals[p];
                Vector3 previewWorld = pos + rotation * previewLocal;

                SnapPointType previewType = SnapPointType.Generic;
                if (previewSnapTypes != null && p < previewSnapTypes.Count)
                    previewType = previewSnapTypes[p];

                foreach (var placedMarker in placedMarkers)
                {
                    if (!AreSnapTypesCompatible(previewType, placedMarker.SnapType))
                        continue;

                    Vector3 placedWorld = placedMarker.WorldPosition;
                    float dist = Vector3.Distance(previewWorld, placedWorld);

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestPos = placedWorld - rotation * previewLocal;
                        improved = true;
                    }
                }
            }

            return improved;
        }

        private static bool AreSnapTypesCompatible(SnapPointType a, SnapPointType b)
        {
            // Minimal, Valheim-like guardrail: ridge-to-ridge only.
            if (a == SnapPointType.Ridge || b == SnapPointType.Ridge)
                return a == b;

            return true;
        }

        /// <summary>
        /// Expanded support check for multi-story building. Foundations always place freely.
        /// Other pieces must be snapped and have a supporting piece nearby.
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
                if (piece == null) continue;

                var pt = piece.PieceType;

                // Wall: Foundation, Floor, Wall (multi-story stacking)
                if (type == BuildingPieceType.Wall &&
                    (pt == BuildingPieceType.Foundation || pt == BuildingPieceType.Floor || pt == BuildingPieceType.Wall))
                    return true;

                // Floor: Foundation, Wall, Floor
                if (type == BuildingPieceType.Floor &&
                    (pt == BuildingPieceType.Foundation || pt == BuildingPieceType.Wall || pt == BuildingPieceType.Floor))
                    return true;

                // Ramp: Foundation, Floor, Ramp, Wall
                if (type == BuildingPieceType.Ramp &&
                    (pt == BuildingPieceType.Foundation || pt == BuildingPieceType.Floor ||
                     pt == BuildingPieceType.Ramp || pt == BuildingPieceType.Wall))
                    return true;

                // Roof: Wall, Floor, Roof, Foundation
                if (type == BuildingPieceType.Roof26 &&
                    (pt == BuildingPieceType.Wall || pt == BuildingPieceType.Floor ||
                     pt == BuildingPieceType.Roof26 || pt == BuildingPieceType.Foundation))
                    return true;

                // Stairs: Foundation, Floor, Stairs, Wall
                if (type == BuildingPieceType.Stairs &&
                    (pt == BuildingPieceType.Foundation || pt == BuildingPieceType.Floor ||
                     pt == BuildingPieceType.Stairs || pt == BuildingPieceType.Wall))
                    return true;

                // Pole, Beam, AngledWall: any adjacent piece supports
                if (type == BuildingPieceType.Pole || type == BuildingPieceType.Beam ||
                    type == BuildingPieceType.AngledWall)
                    return true;

                // DoorFrame, Window, HalfWall: same as Wall
                if ((type == BuildingPieceType.DoorFrame || type == BuildingPieceType.Window ||
                     type == BuildingPieceType.HalfWall) &&
                    (pt == BuildingPieceType.Foundation || pt == BuildingPieceType.Floor ||
                     pt == BuildingPieceType.Wall || pt == BuildingPieceType.DoorFrame ||
                     pt == BuildingPieceType.Window || pt == BuildingPieceType.HalfWall))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a building piece would overlap/intersect with existing placed pieces.
        /// Valheim-style: uses box overlap at the target position to prevent interpenetration.
        /// </summary>
        /// <param name="pos">Target world position.</param>
        /// <param name="rotation">Target rotation.</param>
        /// <param name="halfExtents">Half-extents of the building piece bounding box.</param>
        /// <param name="overlapBuffer">Pre-allocated collider buffer.</param>
        /// <returns>True if the position is clear (no overlap), false if blocked.</returns>
        public static bool CheckNoOverlap(
            Vector3 pos,
            Quaternion rotation,
            Vector3 halfExtents,
            Collider[] overlapBuffer)
        {
            // Shrink slightly to allow touching but not interpenetrating
            Vector3 shrunk = halfExtents * 0.85f;
            int count = Physics.OverlapBoxNonAlloc(pos, shrunk, overlapBuffer, rotation);

            for (int i = 0; i < count; i++)
            {
                if (overlapBuffer[i] == null) continue;
                var piece = overlapBuffer[i].GetComponentInParent<BuildingPiece>();
                if (piece != null) return false; // Overlapping with existing piece
            }
            return true;
        }

        /// <summary>
        /// Validates placement restrictions (Valheim-style per-piece flags).
        /// </summary>
        public static bool CheckPlacementRestrictions(
            Vector3 pos,
            BuildingPiece pieceTemplate,
            float surfaceAngle)
        {
            if (pieceTemplate == null) return true;

            if (pieceTemplate.OnlyOnFlat && surfaceAngle > 15f)
            {
                Debug.Log($"[BuildingSnap] Placement denied: surface too steep ({surfaceAngle:0.0}° > 15°)");
                return false;
            }

            if (pieceTemplate.GroundPiece)
            {
                // Must have ground beneath within reasonable distance
                if (!Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, 2f))
                {
                    Debug.Log("[BuildingSnap] Placement denied: no ground beneath (groundPiece)");
                    return false;
                }
            }

            return true;
        }
    }
}
