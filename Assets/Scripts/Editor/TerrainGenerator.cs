using UnityEngine;
using UnityEditor;
using System.IO;

namespace ByteWar.Editor
{
    /// <summary>
    /// Generates a high-quality procedural terrain with multi-octave FBM heightmap,
    /// 4 texture layers blended by slope/height, and 300+ trees.
    /// Saves all assets to disk; scene placement is handled by SceneGenerator.
    /// </summary>
    public static class TerrainGenerator
    {
        private const string BasePath = "Assets/GeneratedPrefabs";
        private const string TdPath = "Assets/GeneratedPrefabs/GeneratedTerrainData.asset";
        private const string TreePrefabPath = "Assets/GeneratedPrefabs/TreePrefab.prefab";

        [MenuItem("ByteWar/Generate Terrain")]
        public static void GenerateTerrain()
        {
            Debug.Log("[TerrainGenerator] Starting high-quality terrain generation...");
            EnsureFolder(BasePath);

            // ── Step 1: Tree prefab (must exist before TerrainData references it) ──────
            GameObject treePrefab = GenerateTreePrefab();
            Debug.Log("[TerrainGenerator] Tree prefab ready.");

            // ── Step 2: Terrain Layers (4 biomes) saved as assets ────────────────────
            TerrainLayer[] layers = new TerrainLayer[]
            {
                MakeTerrainLayer("LayerGrass",  new Color(0.22f, 0.52f, 0.16f), new Color(0.35f, 0.62f, 0.22f), 0.015f, 20f),
                MakeTerrainLayer("LayerDirt",   new Color(0.48f, 0.32f, 0.16f), new Color(0.60f, 0.44f, 0.24f), 0.020f, 15f),
                MakeTerrainLayer("LayerRock",   new Color(0.42f, 0.40f, 0.38f), new Color(0.58f, 0.56f, 0.52f), 0.025f, 10f),
                MakeTerrainLayer("LayerSnow",   new Color(0.88f, 0.90f, 0.94f), new Color(0.94f, 0.95f, 0.98f), 0.010f,  8f),
            };
            Debug.Log("[TerrainGenerator] 4 terrain layers created.");

            // ── Step 3: Create TerrainData ────────────────────────────────────────────
            TerrainData td = new TerrainData();
            td.heightmapResolution = 513;
            td.size = new Vector3(500f, 65f, 500f);

            // Multi-octave FBM heightmap
            int res = td.heightmapResolution;
            float[,] heights = new float[res, res];
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = x / (float)res;
                    float nz = z / (float)res;
                    float h = FBM(nx, nz, 7, 0.48f, 2.1f, 3.0f);

                    // Flatten a central plateau for the spawn area
                    float dx = nx - 0.5f, dz = nz - 0.5f;
                    float spawnDist = Mathf.Sqrt(dx * dx + dz * dz);
                    float flatten = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.22f, spawnDist));
                    h = Mathf.Lerp(0.04f, h * 0.55f, flatten);

                    heights[z, x] = h;
                }
            }
            td.SetHeights(0, 0, heights);

            // Assign layers so splatmap dimensions are correct
            td.terrainLayers = layers;

            // ── Step 4: Splatmap (blend layers by height & steepness) ─────────────────
            int alpha = td.alphamapResolution;
            float[,,] splatmap = new float[alpha, alpha, 4];
            for (int z = 0; z < alpha; z++)
            {
                for (int x = 0; x < alpha; x++)
                {
                    float nx = x / (float)alpha;
                    float nz = z / (float)alpha;
                    float h = td.GetInterpolatedHeight(nx, nz) / td.size.y;   // 0-1
                    float sl = td.GetSteepness(nx, nz) / 90f;                   // 0-1

                    float grass = Mathf.Clamp01(1f - sl * 3.5f - Mathf.Max(0, h - 0.55f) * 5f);
                    float dirt = Mathf.Clamp01(0.4f - sl * 1.5f - Mathf.Abs(h - 0.25f) * 4f);
                    float rock = Mathf.Clamp01(sl * 2.8f + Mathf.Max(0, h - 0.60f) * 4f);
                    float snow = Mathf.Clamp01(Mathf.Max(0, h - 0.70f) * 10f);

                    float total = grass + dirt + rock + snow + 0.0001f;
                    splatmap[z, x, 0] = grass / total;
                    splatmap[z, x, 1] = dirt / total;
                    splatmap[z, x, 2] = rock / total;
                    splatmap[z, x, 3] = snow / total;
                }
            }
            td.SetAlphamaps(0, 0, splatmap);
            Debug.Log("[TerrainGenerator] Splatmap computed.");

            // ── Step 5: Trees ─────────────────────────────────────────────────────────
            td.treePrototypes = new TreePrototype[]
            {
                new TreePrototype { prefab = treePrefab, bendFactor = 0.25f }
            };

            var trees = new System.Collections.Generic.List<TreeInstance>();
            var rng = new System.Random(12345);
            int attempts = 0;
            while (trees.Count < 400 && attempts < 5000)
            {
                attempts++;
                float tx = (float)rng.NextDouble();
                float tz = (float)rng.NextDouble();
                float h = td.GetInterpolatedHeight(tx, tz) / td.size.y;
                float sl = td.GetSteepness(tx, tz);

                // Avoid spawn area and extreme heights / steep slopes
                float dx = tx - 0.5f, dz = tz - 0.5f;
                if (Mathf.Sqrt(dx * dx + dz * dz) < 0.12f) continue;
                if (h < 0.04f || h > 0.52f || sl > 28f) continue;

                float scale = 0.75f + (float)rng.NextDouble() * 0.55f;
                trees.Add(new TreeInstance
                {
                    position = new Vector3(tx, 0, tz),
                    widthScale = scale,
                    heightScale = scale * (0.9f + (float)rng.NextDouble() * 0.3f),
                    prototypeIndex = 0,
                    color = new Color(0.8f + (float)rng.NextDouble() * 0.2f, 1f, 0.8f),
                    lightmapColor = Color.white
                });
            }
            td.treeInstances = trees.ToArray();
            Debug.Log($"[TerrainGenerator] Placed {trees.Count} trees.");

            // ── Step 6: Save TerrainData ──────────────────────────────────────────────
            DeleteAsset(TdPath);
            AssetDatabase.CreateAsset(td, TdPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TerrainGenerator] TerrainData saved to {TdPath}.");
            Debug.Log("[TerrainGenerator] Terrain generation complete! Run Generate Test Scene to place in scene.");
        }

        // ── Helpers ────────────────────────────────────────────────────────────────────

        /// <summary>Fractional Brownian Motion — layered Perlin for natural terrain.</summary>
        private static float FBM(float x, float y, int octaves, float persistence, float lacunarity, float scale)
        {
            float value = 0f, amplitude = 1f, frequency = scale, maxVal = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float sampleX = x * frequency + i * 127.1f;
                float sampleY = y * frequency + i * 311.7f;
                value += Mathf.PerlinNoise(sampleX, sampleY) * amplitude;
                maxVal += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }
            return value / maxVal;
        }

        private static TerrainLayer MakeTerrainLayer(string name, Color colA, Color colB,
            float noiseScale, float tileSize)
        {
            // Generate a 256×256 noise texture
            string texPath = $"{BasePath}/{name}Tex.png";
            DeleteAsset(texPath);
            var tex = new Texture2D(256, 256, TextureFormat.RGB24, true);
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float n = Mathf.PerlinNoise(x * noiseScale * 256f, y * noiseScale * 256f);
                    n = Mathf.Clamp01(n + 0.4f * Mathf.PerlinNoise(x * noiseScale * 512f, y * noiseScale * 512f));
                    tex.SetPixel(x, y, Color.Lerp(colA, colB, Mathf.Clamp01(n)));
                }
            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);

            var ti = new TextureImporter();
            Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            string layPath = $"{BasePath}/{name}.terrainlayer";
            DeleteAsset(layPath);
            var layer = new TerrainLayer
            {
                diffuseTexture = imported,
                tileSize = new Vector2(tileSize, tileSize),
                smoothness = 0.0f,
                metallic = 0.0f
            };
            AssetDatabase.CreateAsset(layer, layPath);
            return AssetDatabase.LoadAssetAtPath<TerrainLayer>(layPath);
        }

        private static GameObject GenerateTreePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath);
            if (existing != null) return existing;

            GameObject root = new GameObject("TreePrefab");

            // Trunk: brown cylinder
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(root.transform);
            trunk.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            trunk.transform.localScale = new Vector3(0.28f, 1.8f, 0.28f);
            Object.DestroyImmediate(trunk.GetComponent<Collider>());
            var trunkMat = new Material(Shader.Find("Standard")) { color = new Color(0.32f, 0.18f, 0.07f) };
            trunkMat.SetFloat("_Glossiness", 0.1f);
            trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;

            // Three canopy spheres stacked — gives a lush, layered look
            float[] canopyY = { 4.0f, 5.4f, 6.5f };
            float[] canopyScale = { 2.6f, 2.1f, 1.5f };
            Color[] canopyColors =
            {
                new Color(0.08f, 0.38f, 0.06f),
                new Color(0.10f, 0.44f, 0.08f),
                new Color(0.14f, 0.52f, 0.10f),
            };
            for (int i = 0; i < 3; i++)
            {
                var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                canopy.name = $"Canopy{i}";
                canopy.transform.SetParent(root.transform);
                canopy.transform.localPosition = new Vector3(0f, canopyY[i], 0f);
                float s = canopyScale[i];
                canopy.transform.localScale = new Vector3(s * 1.15f, s, s * 1.15f);
                Object.DestroyImmediate(canopy.GetComponent<Collider>());
                var mat = new Material(Shader.Find("Standard")) { color = canopyColors[i] };
                mat.SetFloat("_Glossiness", 0.05f);
                canopy.GetComponent<Renderer>().sharedMaterial = mat;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, TreePrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[TerrainGenerator] Tree prefab saved to {TreePrefabPath}");
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

        private static void DeleteAsset(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
        }
    }
}