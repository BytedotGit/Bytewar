using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

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
        private const string LargeTreeGeneratedPrefabPath = "Assets/Resources/Generated/LargeTree/LargeTree.prefab";
        private const string LargeTreeVariationProfilePath = "Assets/GeneratedPrefabs/LargeTreeVariationProfile.asset";
        private const string LargeTreeVariationArtRoot = "Assets/Art/Environment/Vegetation/LargeTree/Variants";
        private const string LargeTreeVariationGeneratedRoot = "Assets/Resources/Generated/LargeTree/Variants";
        private const string DetailGrassTexturePath = "Assets/GeneratedPrefabs/DetailGrassTex.png";
        private const float MainPathHalfWidthNormalized = 0.020f;
        private const float BranchPathHalfWidthNormalized = 0.016f;
        private static readonly Vector2[] MainPathPointsNormalized =
        {
            new Vector2(0.325f, 0.375f),
            new Vector2(0.440f, 0.460f),
            new Vector2(0.550f, 0.525f),
            new Vector2(0.660f, 0.600f),
        };
        private static readonly Vector2[] BranchPathPointsNormalizedA =
        {
            new Vector2(0.440f, 0.460f),
            new Vector2(0.485f, 0.610f),
            new Vector2(0.610f, 0.690f),
        };
        private static readonly Vector2[] BranchPathPointsNormalizedB =
        {
            new Vector2(0.440f, 0.460f),
            new Vector2(0.360f, 0.590f),
        };
        private static readonly string[] LargeTreeVariationNames =
        {
            "Seedling",
            "Sapling",
            "Young",
            "Mature",
            "Adult",
        };

        private static readonly string[] LegacyLargeTreeVariationNames =
        {
            "BroadleafClassic",
            "BroadleafWide",
            "BroadleafTall",
            "PineA",
            "PineClustered",
        };

        [MenuItem("ByteWar/Generate Terrain")]
        public static void GenerateTerrain()
        {
            Debug.Log("[TerrainGenerator] Starting high-quality terrain generation...");
            EnsureFolder(BasePath);

            // Attempt to materialize runtime-ready variation prefabs before building terrain prototypes.
            LargeTreeVariationPrefabGenerator.GenerateAll();

            // ── Step 1: Terrain tree prototypes are intentionally disabled ───────────
            // Quaternius world scatter now drives environment visuals and Building UI parity.
            Debug.Log("[TerrainGenerator] Terrain tree prototypes disabled; scene uses Quaternius world scatter instead.");

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

                    float cx = (nx - 0.5f) * 2f;
                    float cz = (nz - 0.5f) * 2f;
                    float radial = Mathf.Clamp01(Mathf.Sqrt((cx * cx) + (cz * cz)));
                    float terrainNoise = Mathf.PerlinNoise(nx * 3.8f + 17.4f, nz * 3.8f + 41.8f);
                    float pathMask = EvaluatePathMaskNormalized(nx, nz);

                    float meadow = Mathf.Clamp01((1f - radial) * 0.90f + (0.42f - h) * 1.45f + (terrainNoise - 0.5f) * 0.30f);
                    float highland = Mathf.Clamp01((cz + 0.25f) * 0.90f + (h - 0.42f) * 1.85f + (terrainNoise - 0.5f) * 0.38f);
                    float woodland = Mathf.Clamp01(0.48f + (1f - Mathf.Abs(cx)) * 0.32f + (terrainNoise - 0.5f) * 0.42f - highland * 0.20f);

                    float grass = Mathf.Clamp01(0.62f + meadow * 0.45f + woodland * 0.25f - sl * 0.55f - highland * 0.24f - pathMask * 0.22f);
                    float dirt = Mathf.Clamp01(0.09f + pathMask * 1.55f + meadow * 0.14f + Mathf.Clamp01(0.25f - h) * 0.20f);
                    float rock = Mathf.Clamp01(0.10f + sl * 1.45f + highland * 0.66f + Mathf.Clamp01(h - 0.58f) * 0.85f - meadow * 0.15f - pathMask * 0.35f);
                    float snow = Mathf.Clamp01((h - 0.78f) * 5.0f + highland * 0.25f - pathMask * 0.45f);

                    grass = Mathf.Max(grass, 0.06f);

                    float total = grass + dirt + rock + snow + 0.0001f;
                    splatmap[z, x, 0] = grass / total;
                    splatmap[z, x, 1] = dirt / total;
                    splatmap[z, x, 2] = rock / total;
                    splatmap[z, x, 3] = snow / total;
                }
            }
            td.SetAlphamaps(0, 0, splatmap);
            SetupDetailGrass(td, splatmap);
            Debug.Log("[TerrainGenerator] Splatmap and grass details computed.");

            // ── Step 5: Trees (disabled) ─────────────────────────────────────────────
            td.treePrototypes = Array.Empty<TreePrototype>();
            td.treeInstances = Array.Empty<TreeInstance>();
            Debug.Log("[TerrainGenerator] Cleared terrain tree instances.");

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

        private static void SetupDetailGrass(TerrainData td, float[,,] splatmap)
        {
            if (td == null || splatmap == null)
                return;

            Texture2D detailTexture = CreateOrLoadDetailGrassTexture();
            if (detailTexture == null)
            {
                Debug.LogWarning("[TerrainGenerator] Grass detail texture unavailable, skipping detail grass generation.");
                return;
            }

            td.SetDetailResolution(1024, 16);
            td.detailPrototypes = new[]
            {
                new DetailPrototype
                {
                    prototypeTexture = detailTexture,
                    renderMode = DetailRenderMode.GrassBillboard,
                    healthyColor = new Color(0.30f, 0.62f, 0.24f, 1f),
                    dryColor = new Color(0.43f, 0.56f, 0.28f, 1f),
                    minWidth = 0.55f,
                    maxWidth = 1.30f,
                    minHeight = 0.60f,
                    maxHeight = 1.45f,
                    noiseSpread = 0.20f,
                    usePrototypeMesh = false,
                },
            };

            int width = td.detailWidth;
            int height = td.detailHeight;
            int[,] density = new int[height, width];

            for (int y = 0; y < height; y++)
            {
                float nz = y / (float)Mathf.Max(1, height - 1);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)Mathf.Max(1, width - 1);
                    float grassWeight = SampleSplatWeight(splatmap, td.alphamapWidth, td.alphamapHeight, nx, nz, 0);
                    float steepness = td.GetSteepness(nx, nz) / 90f;
                    float pathMask = EvaluatePathMaskNormalized(nx, nz);

                    float detailNoise = Mathf.PerlinNoise(nx * 24f + 1.7f, nz * 24f + 7.9f);
                    float densityFloat = (grassWeight * 24f) + ((1f - pathMask) * 10f) + ((detailNoise - 0.5f) * 4f);
                    densityFloat -= steepness * 16f;

                    if (pathMask > 0.35f)
                        densityFloat *= 0.22f;
                    if (steepness > 0.45f)
                        densityFloat *= 0.35f;

                    density[y, x] = Mathf.Clamp(Mathf.RoundToInt(densityFloat), 0, 36);
                }
            }

            td.SetDetailLayer(0, 0, 0, density);
        }

        private static Texture2D CreateOrLoadDetailGrassTexture()
        {
            DeleteAsset(DetailGrassTexturePath);

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = x / (float)(size - 1);
                    float ny = y / (float)(size - 1);
                    float centerDist = Vector2.Distance(new Vector2(nx, ny), Vector2.one * 0.5f);
                    float feather = Mathf.Clamp01(1f - (centerDist * 2.05f));
                    float noise = Mathf.PerlinNoise(nx * 6.2f + 0.9f, ny * 6.2f + 4.1f);
                    float alpha = Mathf.Clamp01((feather * 0.88f) + (noise * 0.22f));
                    tex.SetPixel(x, y, new Color(0.33f, 0.66f, 0.30f, alpha));
                }
            }

            tex.Apply();
            File.WriteAllBytes(DetailGrassTexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(DetailGrassTexturePath, ImportAssetOptions.ForceUpdate);

            return AssetDatabase.LoadAssetAtPath<Texture2D>(DetailGrassTexturePath);
        }

        private static float SampleSplatWeight(float[,,] splatmap, int alphaWidth, int alphaHeight, float nx, float nz, int channel)
        {
            if (splatmap == null)
                return 0f;

            int x = Mathf.Clamp(Mathf.RoundToInt(nx * Mathf.Max(1, alphaWidth - 1)), 0, Mathf.Max(0, alphaWidth - 1));
            int z = Mathf.Clamp(Mathf.RoundToInt(nz * Mathf.Max(1, alphaHeight - 1)), 0, Mathf.Max(0, alphaHeight - 1));
            return splatmap[z, x, channel];
        }

        private static float EvaluatePathMaskNormalized(float nx, float nz)
        {
            Vector2 point = new Vector2(nx, nz);

            float distanceMain = DistanceToPathChainNormalized(point, MainPathPointsNormalized);
            float distanceBranchA = DistanceToPathChainNormalized(point, BranchPathPointsNormalizedA);
            float distanceBranchB = DistanceToPathChainNormalized(point, BranchPathPointsNormalizedB);

            float pathDistance = Mathf.Min(distanceMain, Mathf.Min(distanceBranchA, distanceBranchB));
            float baseWidth = pathDistance == distanceMain ? MainPathHalfWidthNormalized : BranchPathHalfWidthNormalized;

            float widthNoise = Mathf.PerlinNoise(nx * 13.5f + 2.2f, nz * 13.5f + 6.4f);
            float effectiveWidth = baseWidth * Mathf.Lerp(0.80f, 1.30f, widthNoise);
            return Mathf.Clamp01(1f - (pathDistance / Mathf.Max(0.0001f, effectiveWidth)));
        }

        private static float DistanceToPathChainNormalized(Vector2 point, Vector2[] points)
        {
            if (points == null || points.Length < 2)
                return float.MaxValue;

            float minDistance = float.MaxValue;
            for (int i = 1; i < points.Length; i++)
            {
                float distance = DistancePointToSegment(point, points[i - 1], points[i]);
                if (distance < minDistance)
                    minDistance = distance;
            }

            return minDistance;
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 segmentA, Vector2 segmentB)
        {
            Vector2 ab = segmentB - segmentA;
            float abSqr = ab.sqrMagnitude;
            if (abSqr <= 0.00001f)
                return Vector2.Distance(point, segmentA);

            float t = Mathf.Clamp01(Vector2.Dot(point - segmentA, ab) / abSqr);
            Vector2 closest = segmentA + (ab * t);
            return Vector2.Distance(point, closest);
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

        public static int SelectWeightedPrototypeIndex(System.Random rng, IReadOnlyList<float> weights)
        {
            if (rng == null)
                throw new ArgumentNullException(nameof(rng));

            if (weights == null || weights.Count == 0)
                return 0;

            float totalWeight = 0f;
            for (int i = 0; i < weights.Count; i++)
                totalWeight += Mathf.Max(0f, weights[i]);

            if (totalWeight <= 0f)
                return 0;

            float roll = (float)rng.NextDouble() * totalWeight;
            float cumulative = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                cumulative += Mathf.Max(0f, weights[i]);
                if (roll <= cumulative)
                    return i;
            }

            return weights.Count - 1;
        }

        private static TerrainTreePrototypeSet ResolveTerrainTreePrototypeSet()
        {
            var prototypes = new List<TreePrototype>();
            var weights = new List<float>();

            var profile = LoadOrCreateLargeTreeVariationProfile();
            if (profile != null)
            {
                foreach (var entry in profile.Entries)
                {
                    if (entry == null || entry.Weight <= 0f)
                        continue;

                    GameObject prefab = entry.Prefab;
                    if (prefab == null)
                        prefab = TryLoadLargeTreeVariationPrefab(entry.VariationName);
                    if (prefab == null)
                        continue;

                    prototypes.Add(new TreePrototype { prefab = prefab, bendFactor = 0.25f });
                    weights.Add(entry.Weight);
                }
            }

            if (prototypes.Count > 0)
                return new TerrainTreePrototypeSet(prototypes.ToArray(), weights.ToArray());

            var fallbackPrefab = ResolveTerrainTreeFallbackPrefab();
            return new TerrainTreePrototypeSet(
                new[] { new TreePrototype { prefab = fallbackPrefab, bendFactor = 0.25f } },
                new[] { 1f });
        }

        private static LargeTreeVariationProfile LoadOrCreateLargeTreeVariationProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<LargeTreeVariationProfile>(LargeTreeVariationProfilePath);
            if (profile != null)
            {
                if (NeedsGrowthStageProfileMigration(profile.Entries))
                {
                    profile.SetEntries(BuildDefaultLargeTreeVariationEntries());
                    EditorUtility.SetDirty(profile);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[TerrainGenerator] Migrated LargeTree variation profile to growth stages at '{LargeTreeVariationProfilePath}'.");
                }

                return profile;
            }

            EnsureFolder(BasePath);

            profile = ScriptableObject.CreateInstance<LargeTreeVariationProfile>();
            profile.SetEntries(BuildDefaultLargeTreeVariationEntries());

            AssetDatabase.CreateAsset(profile, LargeTreeVariationProfilePath);
            AssetDatabase.ImportAsset(LargeTreeVariationProfilePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();

            Debug.Log($"[TerrainGenerator] Created LargeTree variation profile at '{LargeTreeVariationProfilePath}'.");
            return profile;
        }

        private static List<LargeTreeVariationProfileEntry> BuildDefaultLargeTreeVariationEntries()
        {
            return new List<LargeTreeVariationProfileEntry>
            {
                new LargeTreeVariationProfileEntry("Seedling", TryLoadLargeTreeVariationPrefab("Seedling"), 0.16f),
                new LargeTreeVariationProfileEntry("Sapling", TryLoadLargeTreeVariationPrefab("Sapling"), 0.24f),
                new LargeTreeVariationProfileEntry("Young", TryLoadLargeTreeVariationPrefab("Young"), 0.28f),
                new LargeTreeVariationProfileEntry("Mature", TryLoadLargeTreeVariationPrefab("Mature"), 0.20f),
                new LargeTreeVariationProfileEntry("Adult", TryLoadLargeTreeVariationPrefab("Adult"), 0.12f),
            };
        }

        private static bool NeedsGrowthStageProfileMigration(IReadOnlyList<LargeTreeVariationProfileEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return true;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;

                string name = entry.VariationName?.Trim();
                if (string.IsNullOrEmpty(name))
                    continue;

                if (string.Equals(name, "LargeTreeBase", StringComparison.OrdinalIgnoreCase))
                    return true;

                for (int legacyIndex = 0; legacyIndex < LegacyLargeTreeVariationNames.Length; legacyIndex++)
                {
                    if (string.Equals(name, LegacyLargeTreeVariationNames[legacyIndex], StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                seen.Add(name);
            }

            for (int i = 0; i < LargeTreeVariationNames.Length; i++)
            {
                if (!seen.Contains(LargeTreeVariationNames[i]))
                    return true;
            }

            return false;
        }

        private static GameObject TryLoadLargeTreeVariationPrefab(string variationName)
        {
            if (string.IsNullOrWhiteSpace(variationName) || variationName.Equals("LargeTreeBase", StringComparison.OrdinalIgnoreCase))
                return null;

            string generatedPath = $"{LargeTreeVariationGeneratedRoot}/{variationName}/LargeTree_{variationName}.prefab";
            var generatedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(generatedPath);
            if (generatedPrefab != null)
                return generatedPrefab;

            string artPath = $"{LargeTreeVariationArtRoot}/{variationName}/LargeTree_{variationName}.fbx";
            return AssetDatabase.LoadAssetAtPath<GameObject>(artPath);
        }

        private static GameObject ResolveTerrainTreeFallbackPrefab()
        {
            var generatedLargeTree = AssetDatabase.LoadAssetAtPath<GameObject>(LargeTreeGeneratedPrefabPath);
            if (generatedLargeTree != null)
            {
                Debug.Log($"[TerrainGenerator] Using generated LargeTree prefab at '{LargeTreeGeneratedPrefabPath}' for terrain prototypes.");
                return generatedLargeTree;
            }

            Debug.LogWarning($"[TerrainGenerator] Generated LargeTree prefab not found at '{LargeTreeGeneratedPrefabPath}'. Falling back to procedural TreePrefab.");
            return GenerateTreePrefab();
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

        private readonly struct TerrainTreePrototypeSet
        {
            public readonly TreePrototype[] Prototypes;
            public readonly float[] Weights;

            public TerrainTreePrototypeSet(TreePrototype[] prototypes, float[] weights)
            {
                Prototypes = prototypes;
                Weights = weights;
            }
        }
    }

}