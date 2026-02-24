using UnityEngine;

namespace ByteWar.Editor
{
    /// <summary>
    /// Procedural mesh generators for non-cube building piece visuals.
    /// Used by PrefabGenerator at edit-time; meshes are saved as assets.
    /// </summary>
    public static class BuildingMeshBuilder
    {
        /// <summary>
        /// Builds a ramp (wedge) mesh. The slope rises from the front edge (-Z) at y=0
        /// to the back edge (+Z) at y=height.
        /// </summary>
        public static Mesh BuildRampMesh(float width, float height, float depth)
        {
            float hw = width * 0.5f;
            float hd = depth * 0.5f;

            // 6 unique corner positions of the wedge
            Vector3 blf = new Vector3(-hw, 0, -hd);     // bottom-left-front
            Vector3 brf = new Vector3(hw, 0, -hd);      // bottom-right-front
            Vector3 blb = new Vector3(-hw, 0, hd);      // bottom-left-back
            Vector3 brb = new Vector3(hw, 0, hd);       // bottom-right-back
            Vector3 tlb = new Vector3(-hw, height, hd); // top-left-back
            Vector3 trb = new Vector3(hw, height, hd);  // top-right-back

            // Duplicate vertices per face for flat shading normals
            Vector3[] vertices = new Vector3[]
            {
                // Bottom face (normal -Y) [0-3]
                blf, blb, brb, brf,
                // Slope face (normal up-forward) [4-7]
                blf, brf, trb, tlb,
                // Back wall (normal +Z) [8-11]
                blb, tlb, trb, brb,
                // Left triangle (normal -X) [12-14]
                blf, tlb, blb,
                // Right triangle (normal +X) [15-17]
                brf, brb, trb,
            };

            int[] triangles = new int[]
            {
                // Bottom (viewed from -Y, CW = outward)
                0, 1, 2,  0, 2, 3,
                // Slope (viewed from above-front, CW = outward)
                4, 7, 6,  4, 6, 5,
                // Back wall (viewed from +Z, CW = outward)
                8, 9, 10,  8, 10, 11,
                // Left triangle (viewed from -X, CW = outward)
                12, 13, 14,
                // Right triangle (viewed from +X, CW = outward)
                15, 16, 17,
            };

            var mesh = new Mesh { name = "RampMesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Builds a gable roof mesh. Ridge runs along Z-axis, slopes down on ±X sides.
        /// </summary>
        public static Mesh BuildGableRoofMesh(float width, float depth, float ridgeHeight)
        {
            float hw = width * 0.5f;
            float hd = depth * 0.5f;

            Vector3 blf = new Vector3(-hw, 0, -hd);
            Vector3 brf = new Vector3(hw, 0, -hd);
            Vector3 blb = new Vector3(-hw, 0, hd);
            Vector3 brb = new Vector3(hw, 0, hd);
            Vector3 rf = new Vector3(0, ridgeHeight, -hd); // ridge front
            Vector3 rb = new Vector3(0, ridgeHeight, hd);  // ridge back

            Vector3[] vertices = new Vector3[]
            {
                // Left slope [0-3]
                blf, blb, rb, rf,
                // Right slope [4-7]
                brf, rf, rb, brb,
                // Front gable triangle [8-10]
                blf, rf, brf,
                // Back gable triangle [11-13]
                blb, brb, rb,
                // Bottom [14-17]
                blf, brf, brb, blb,
            };

            int[] triangles = new int[]
            {
                // Left slope (outward = -X up)
                0, 1, 2,  0, 2, 3,
                // Right slope (outward = +X up)
                4, 5, 6,  4, 6, 7,
                // Front gable (outward = -Z)
                8, 9, 10,
                // Back gable (outward = +Z)
                11, 12, 13,
                // Bottom (outward = -Y)
                14, 15, 16,  14, 16, 17,
            };

            var mesh = new Mesh { name = "GableRoofMesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Builds an angled wall (triangular prism) mesh. Triangle cross-section in XY plane,
        /// extruded along Z with the given thickness.
        /// Base along X, peak at center top.
        /// </summary>
        public static Mesh BuildAngledWallMesh(float width, float peakHeight, float thickness)
        {
            float hw = width * 0.5f;
            float ht = thickness * 0.5f;

            // Front face (z = -ht)
            Vector3 flb = new Vector3(-hw, 0, -ht);
            Vector3 frb = new Vector3(hw, 0, -ht);
            Vector3 fp = new Vector3(0, peakHeight, -ht);

            // Back face (z = +ht)
            Vector3 blb = new Vector3(-hw, 0, ht);
            Vector3 brb = new Vector3(hw, 0, ht);
            Vector3 bp = new Vector3(0, peakHeight, ht);

            Vector3[] vertices = new Vector3[]
            {
                // Front triangle [0-2]
                flb, fp, frb,
                // Back triangle [3-5]
                blb, brb, bp,
                // Bottom quad [6-9]
                flb, frb, brb, blb,
                // Left slope quad [10-13]
                flb, blb, bp, fp,
                // Right slope quad [14-17]
                frb, fp, bp, brb,
            };

            int[] triangles = new int[]
            {
                // Front (outward = -Z)
                0, 1, 2,
                // Back (outward = +Z)
                3, 4, 5,
                // Bottom (outward = -Y)
                6, 9, 8,  6, 8, 7,
                // Left slope (outward = left-up)
                10, 11, 12,  10, 12, 13,
                // Right slope (outward = right-up)
                14, 15, 16,  14, 16, 17,
            };

            var mesh = new Mesh { name = "AngledWallMesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
