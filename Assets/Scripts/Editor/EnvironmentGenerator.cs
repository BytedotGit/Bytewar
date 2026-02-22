using UnityEngine;
using UnityEditor;

namespace ByteWar.Editor
{
    /// <summary>
    /// Generates and scatters environment props (rocks, boulders, fallen logs) across the terrain.
    /// Call after TerrainGenerator and before building.
    /// </summary>
    public static class EnvironmentGenerator
    {
        private const string BasePath = "Assets/GeneratedPrefabs";
        private const string RockPrefabPath = "Assets/GeneratedPrefabs/RockCluster.prefab";
        private const string BoulderPrefabPath = "Assets/GeneratedPrefabs/Boulder.prefab";
        private const string LogPrefabPath = "Assets/GeneratedPrefabs/FallenLog.prefab";

        [MenuItem("ByteWar/Generate Environment Objects")]
        public static void GenerateEnvironment()
        {
            Debug.Log("[EnvironmentGenerator] Starting environment object generation...");
            EnsureFolder(BasePath);

            GameObject rockPrefab = GenerateRockClusterPrefab();
            GameObject boulderPrefab = GenerateBoulderPrefab();
            GameObject logPrefab = GenerateFallenLogPrefab();

            // Scatter in the active scene (called from SceneGenerator after terrain is placed)
            ScatterOnTerrain(rockPrefab, 120, 0.05f, 0.60f, 30f, 0.7f, 1.4f, 42);
            ScatterOnTerrain(boulderPrefab, 40, 0.03f, 0.65f, 35f, 0.8f, 2.0f, 77);
            ScatterOnTerrain(logPrefab, 50, 0.04f, 0.45f, 20f, 0.8f, 1.3f, 99);

            Debug.Log("[EnvironmentGenerator] Environment objects scattered.");
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  Prefab generators
        // ──────────────────────────────────────────────────────────────────────────

        private static GameObject GenerateRockClusterPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RockPrefabPath) != null)
                AssetDatabase.DeleteAsset(RockPrefabPath);

            var rock = Mat("RockMat", new Color(0.45f, 0.44f, 0.40f), 0.05f, 0.15f);
            var root = new GameObject("RockCluster");

            // 3–5 deformed spheres at varying positions / scales / rotations
            float[][] configs = new float[][]
            {
                new float[] { 0f,    0.20f,  0f,    1.00f, 0.82f, 0.90f },
                new float[] {-0.45f, 0.16f,  0.18f, 0.70f, 0.58f, 0.64f },
                new float[] { 0.38f, 0.14f, -0.20f, 0.60f, 0.50f, 0.58f },
                new float[] { 0.15f, 0.10f,  0.38f, 0.50f, 0.42f, 0.48f },
                new float[] {-0.22f, 0.08f, -0.35f, 0.44f, 0.36f, 0.40f },
            };

            for (int i = 0; i < configs.Length; i++)
            {
                var c = configs[i];
                var piece = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                piece.name = $"Rock{i}";
                piece.transform.SetParent(root.transform);
                piece.transform.localPosition = new Vector3(c[0], c[1], c[2]);
                piece.transform.localScale = new Vector3(c[3], c[4], c[5]);
                piece.transform.localRotation = Quaternion.Euler(
                    Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
                piece.GetComponent<Renderer>().sharedMaterial = rock;
                Object.DestroyImmediate(piece.GetComponent<Collider>());
            }

            // Single collider on root
            var sc = root.AddComponent<SphereCollider>();
            sc.radius = 0.55f;
            sc.center = new Vector3(0f, 0.25f, 0f);

            return SavePrefab(root, RockPrefabPath);
        }

        private static GameObject GenerateBoulderPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BoulderPrefabPath) != null)
                AssetDatabase.DeleteAsset(BoulderPrefabPath);

            var rock = Mat("BoulderMat", new Color(0.38f, 0.36f, 0.32f), 0.05f, 0.10f);
            var root = new GameObject("Boulder");

            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0f, 0.50f, 0f);
            body.transform.localScale = new Vector3(1.20f, 0.95f, 1.10f);
            body.GetComponent<Renderer>().sharedMaterial = rock;
            Object.DestroyImmediate(body.GetComponent<Collider>());

            var flat = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flat.name = "FlatSide";
            flat.transform.SetParent(root.transform);
            flat.transform.localPosition = new Vector3(0.30f, 0.48f, 0.20f);
            flat.transform.localScale = new Vector3(0.90f, 0.78f, 0.85f);
            flat.GetComponent<Renderer>().sharedMaterial = rock;
            Object.DestroyImmediate(flat.GetComponent<Collider>());

            var sc = root.AddComponent<SphereCollider>();
            sc.radius = 0.55f;
            sc.center = new Vector3(0f, 0.50f, 0f);

            return SavePrefab(root, BoulderPrefabPath);
        }

        private static GameObject GenerateFallenLogPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LogPrefabPath) != null)
                AssetDatabase.DeleteAsset(LogPrefabPath);

            var logMat = Mat("LogMat", new Color(0.30f, 0.18f, 0.08f), 0.00f, 0.12f);
            var mossMat = Mat("MossMat", new Color(0.18f, 0.35f, 0.12f), 0.00f, 0.10f);
            var root = new GameObject("FallenLog");

            // Horizontal cylinder trunk
            var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = "Log";
            log.transform.SetParent(root.transform);
            log.transform.localPosition = new Vector3(0f, 0.20f, 0f);
            log.transform.localScale = new Vector3(0.30f, 1.60f, 0.30f);
            log.transform.localRotation = Quaternion.Euler(90f, 0f, 15f);
            log.GetComponent<Renderer>().sharedMaterial = logMat;
            Object.DestroyImmediate(log.GetComponent<Collider>());

            // Moss patches on top
            for (int i = 0; i < 4; i++)
            {
                var moss = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                moss.name = $"Moss{i}";
                moss.transform.SetParent(root.transform);
                float xPos = -0.90f + i * 0.60f;
                moss.transform.localPosition = new Vector3(xPos, 0.34f, 0f);
                moss.transform.localScale = new Vector3(0.30f, 0.10f, 0.24f);
                moss.GetComponent<Renderer>().sharedMaterial = mossMat;
                Object.DestroyImmediate(moss.GetComponent<Collider>());
            }

            var cc = root.AddComponent<CapsuleCollider>();
            cc.direction = 2; // Z axis
            cc.center = new Vector3(0f, 0.20f, 0f);
            cc.radius = 0.20f;
            cc.height = 3.40f;

            return SavePrefab(root, LogPrefabPath);
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  Scatter helper
        // ──────────────────────────────────────────────────────────────────────────

        private static void ScatterOnTerrain(GameObject prefab, int count,
            float minNormHeight, float maxNormHeight, float maxSteepness,
            float minScale, float maxScale, int seed)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[EnvironmentGenerator] Prefab is null, skipping scatter.");
                return;
            }

            Terrain terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogWarning("[EnvironmentGenerator] No terrain in scene. Scatter will use flat plane.");
            }

            var rng = new System.Random(seed);
            int placed = 0, attempts = 0;
            const int MaxAttempts = 3000;

            while (placed < count && attempts < MaxAttempts)
            {
                attempts++;
                float nx = (float)rng.NextDouble();
                float nz = (float)rng.NextDouble();

                float worldX = nx * 500f - 250f;
                float worldZ = nz * 500f - 250f;
                float worldY = 0f;

                if (terrain != null)
                {
                    float normH = terrain.terrainData.GetInterpolatedHeight(nx, nz) / terrain.terrainData.size.y;
                    float steep = terrain.terrainData.GetSteepness(nx, nz);
                    if (normH < minNormHeight || normH > maxNormHeight || steep > maxSteepness)
                        continue;

                    // Keep clear of spawn zone (±30m from origin)
                    if (Mathf.Abs(worldX) < 30f && Mathf.Abs(worldZ) < 30f) continue;

                    worldY = terrain.SampleHeight(new Vector3(worldX, 0f, worldZ));
                }

                float scale = (float)(minScale + rng.NextDouble() * (maxScale - minScale));
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = new Vector3(worldX, worldY, worldZ);
                go.transform.localScale = Vector3.one * scale;
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                placed++;
            }

            Debug.Log($"[EnvironmentGenerator] Placed {placed}/{count} {prefab.name} objects (seed={seed}).");
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  Utilities
        // ──────────────────────────────────────────────────────────────────────────

        private static Material Mat(string name, Color color, float metallic, float smoothness)
        {
            string path = $"{BasePath}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                AssetDatabase.DeleteAsset(path);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string child = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
