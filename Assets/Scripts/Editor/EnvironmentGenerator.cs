using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Object = UnityEngine.Object;

namespace ByteWar.Editor
{
    /// <summary>
    /// Generates and scatters premade Quaternius environment props across the terrain.
    /// Call after TerrainGenerator and before building.
    /// </summary>
    public static class EnvironmentGenerator
    {
        private const string BasePath = "Assets/GeneratedPrefabs";
        private const string RockPrefabPath = "Assets/GeneratedPrefabs/RockCluster.prefab";
        private const string BoulderPrefabPath = "Assets/GeneratedPrefabs/Boulder.prefab";
        private const string LogPrefabPath = "Assets/GeneratedPrefabs/FallenLog.prefab";
        private const string LargeTreeVariationPrefabRoot = "Assets/Resources/Generated/LargeTree/Variants";
        private const string QuaterniusUltimateNatureRoot = "Assets/Art/Premade/Quaternius/UltimateNature/FBX";
        private const string QuaterniusUltimateCropsRoot = "Assets/Art/Premade/Quaternius/UltimateCrops/FBX";
        private const string QuaterniusStylizedNatureMegaKitTreesRoot = "Assets/Art/Premade/Quaternius/Environment/Vegetation";
        private const int QuaterniusDecorTreeScatterCount = 620;
        private const int QuaterniusDecorVegetationScatterCount = 1600;
        private const int QuaterniusDecorRockScatterCount = 420;
        private const float ShowcaseTreeSpacing = 8.0f;
        private const float ShowcaseVegetationSpacing = 4.0f;
        private const float ShowcaseRockSpacing = 5.5f;
        private static readonly Vector2 ShowcaseTreesStart = new Vector2(-64f, 56f);
        private static readonly Vector2 ShowcaseVegetationStart = new Vector2(-64f, 12f);
        private static readonly Vector2 ShowcaseRocksStart = new Vector2(28f, 56f);
        private static readonly string[] LargeTreeGrowthStages = { "Seedling", "Sapling", "Young", "Mature", "Adult" };
        private static readonly string[] QuaterniusTreeNameTokens = { "tree" };
        private static readonly string[] QuaterniusTreeExcludeTokens = Array.Empty<string>();
        private static readonly string[] QuaterniusVegetationExcludeTokens = { "tree", "rock", "boulder", "stone", "cliff", "ore" };
        private static readonly string[] QuaterniusRockNameTokens = { "rock", "stone", "boulder", "cliff", "ore" };
        private static readonly string[] QuaterniusRockExcludeTokens = { "tree", "plant", "grass", "leaf", "bush", "flower", "cactus", "crop", "mushroom", "vine" };
        private static readonly Vector2[] MainPathPointsNormalized =
        {
            new Vector2(-0.35f, -0.25f),
            new Vector2(-0.12f, -0.08f),
            new Vector2(0.10f, 0.05f),
            new Vector2(0.32f, 0.20f),
        };
        private static readonly Vector2[] BranchPathPointsNormalizedA =
        {
            new Vector2(-0.12f, -0.08f),
            new Vector2(-0.03f, 0.20f),
            new Vector2(0.22f, 0.35f),
        };
        private static readonly Vector2[] BranchPathPointsNormalizedB =
        {
            new Vector2(-0.12f, -0.08f),
            new Vector2(-0.28f, 0.18f),
        };

        private enum ScatterCategory
        {
            Tree,
            Vegetation,
            Rock,
        }

        [MenuItem("ByteWar/Generate Environment Objects")]
        public static void GenerateEnvironment()
        {
            Debug.Log("[EnvironmentGenerator] Starting environment object generation...");
            EnsureFolder(BasePath);
            CleanupLegacyEnvironmentPlacements();
            DeleteLegacyEnvironmentPrefabs();

            List<GameObject> treeCandidates = GetQuaterniusTreeCandidates();
            List<GameObject> vegetationCandidates = GetQuaterniusVegetationCandidates();
            List<GameObject> rockCandidates = GetQuaterniusRockCandidatesWithFallback();

            int showcaseTrees = PlaceGroupedShowcase(ShowcaseTreesStart, treeCandidates, "Showcase_QuaterniusTree_", columns: 8, spacing: ShowcaseTreeSpacing, minScale: 0.88f, maxScale: 1.15f);
            int showcaseVegetation = PlaceGroupedShowcase(ShowcaseVegetationStart, vegetationCandidates, "Showcase_QuaterniusVegetation_", columns: 14, spacing: ShowcaseVegetationSpacing, minScale: 0.95f, maxScale: 1.05f);
            int showcaseRocks = PlaceGroupedShowcase(ShowcaseRocksStart, rockCandidates, "Showcase_QuaterniusRock_", columns: 8, spacing: ShowcaseRockSpacing, minScale: 0.95f, maxScale: 1.10f);
            Debug.Log($"[EnvironmentGenerator] Quaternius grouped showcase placed: trees={showcaseTrees} vegetation={showcaseVegetation} rocks={showcaseRocks}.");

            int quaterniusRockCount = ScatterQuaterniusRocksAcrossTerrain(rockCandidates, QuaterniusDecorRockScatterCount, 5123);
            int quaterniusTreeCount = ScatterQuaterniusDecorTreesAcrossTerrain(treeCandidates, QuaterniusDecorTreeScatterCount, 2411);
            int quaterniusVegetationCount = ScatterQuaterniusVegetationAcrossTerrain(vegetationCandidates, QuaterniusDecorVegetationScatterCount, 7741);
            Debug.Log($"[EnvironmentGenerator] Quaternius decor placement complete: rocks={quaterniusRockCount} trees={quaterniusTreeCount} vegetation={quaterniusVegetationCount}.");

            Debug.Log("[EnvironmentGenerator] Environment objects scattered.");
        }

        private static int ScatterQuaterniusDecorTreesAcrossTerrain(IReadOnlyList<GameObject> candidates, int count, int seed)
        {
            if (candidates.Count == 0)
            {
                Debug.LogWarning("[EnvironmentGenerator] No Quaternius tree FBX candidates found. Decorative tree scatter skipped.");
                return 0;
            }

            return ScatterPremadeAcrossTerrain(
                candidates,
                "Placed_QuaterniusTree_",
                count,
                minNormHeight: 0.04f,
                maxNormHeight: 0.76f,
                maxSteepness: 36f,
                minScale: 0.70f,
                maxScale: 1.35f,
                seed: seed,
                clearZoneRadius: 8f,
                category: ScatterCategory.Tree);
        }

        private static int ScatterQuaterniusVegetationAcrossTerrain(IReadOnlyList<GameObject> candidates, int count, int seed)
        {
            if (candidates.Count == 0)
            {
                Debug.LogWarning("[EnvironmentGenerator] No Quaternius vegetation FBX candidates found. Decorative vegetation scatter skipped.");
                return 0;
            }

            return ScatterPremadeAcrossTerrain(
                candidates,
                "Placed_QuaterniusVegetation_",
                count,
                minNormHeight: 0.03f,
                maxNormHeight: 0.80f,
                maxSteepness: 40f,
                minScale: 0.80f,
                maxScale: 2.00f,
                seed: seed,
                clearZoneRadius: 5f,
                category: ScatterCategory.Vegetation);
        }

        private static int ScatterQuaterniusRocksAcrossTerrain(IReadOnlyList<GameObject> candidates, int count, int seed)
        {
            if (candidates.Count == 0)
            {
                Debug.LogWarning("[EnvironmentGenerator] No Quaternius rock candidates available (including fallback). Decorative rock scatter skipped.");
                return 0;
            }

            return ScatterPremadeAcrossTerrain(
                candidates,
                "Placed_QuaterniusRock_",
                count,
                minNormHeight: 0.04f,
                maxNormHeight: 0.92f,
                maxSteepness: 55f,
                minScale: 0.85f,
                maxScale: 2.35f,
                seed: seed,
                clearZoneRadius: 7f,
                category: ScatterCategory.Rock);
        }

        private static List<GameObject> GetQuaterniusTreeCandidates()
        {
            // Trees are intentionally constrained to the user-requested pack roots.
            return LoadPremadeAssetCandidates(
                new[] { QuaterniusUltimateNatureRoot, QuaterniusUltimateCropsRoot, QuaterniusStylizedNatureMegaKitTreesRoot },
                QuaterniusTreeNameTokens,
                QuaterniusTreeExcludeTokens);
        }

        private static List<GameObject> GetQuaterniusVegetationCandidates()
        {
            return LoadPremadeAssetCandidates(
                new[] { QuaterniusUltimateCropsRoot, QuaterniusUltimateNatureRoot },
                null,
                QuaterniusVegetationExcludeTokens);
        }

        private static List<GameObject> GetQuaterniusRockCandidatesWithFallback()
        {
            List<GameObject> candidates = LoadPremadeAssetCandidates(
                new[] { QuaterniusUltimateNatureRoot },
                QuaterniusRockNameTokens,
                QuaterniusRockExcludeTokens);

            if (candidates.Count > 0)
                return candidates;

            Debug.LogWarning("[EnvironmentGenerator] No Quaternius rock FBX candidates found. Falling back to generated low-poly rocks.");

            var generatedCandidates = new List<GameObject>();
            GameObject rockCluster = GenerateRockClusterPrefab();
            GameObject boulder = GenerateBoulderPrefab();

            if (rockCluster != null)
                generatedCandidates.Add(rockCluster);
            if (boulder != null)
                generatedCandidates.Add(boulder);

            return generatedCandidates;
        }

        private static int PlaceGroupedShowcase(
            Vector2 start,
            IReadOnlyList<GameObject> candidates,
            string namePrefix,
            int columns,
            float spacing,
            float minScale,
            float maxScale)
        {
            Terrain terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogWarning($"[EnvironmentGenerator] No terrain found for grouped showcase '{namePrefix}'.");
                return 0;
            }

            if (candidates == null || candidates.Count == 0)
                return 0;

            int placed = 0;
            int safeColumns = Mathf.Max(1, columns);
            for (int i = 0; i < candidates.Count; i++)
            {
                GameObject prefab = candidates[i];
                if (prefab == null)
                    continue;

                int column = i % safeColumns;
                int row = i / safeColumns;
                float worldX = start.x + (column * spacing);
                float worldZ = start.y - (row * spacing);
                if (!IsWithinTerrainXZ(terrain, worldX, worldZ))
                    continue;

                float terrainY = terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + terrain.transform.position.y;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (instance == null)
                    continue;

                float lerp = candidates.Count <= 1 ? 0.5f : i / (float)(candidates.Count - 1);
                float scale = Mathf.Lerp(minScale, maxScale, lerp);
                Quaternion uprightCorrection = ComputeUprightCorrectionFromMesh(instance);

                instance.transform.position = new Vector3(worldX, terrainY, worldZ);
                instance.transform.localScale = instance.transform.localScale * scale;
                instance.transform.rotation = uprightCorrection;
                SnapInstanceToTerrain(terrain, instance, worldX, worldZ, 0.015f);

                string sourceLabel = ResolveSourceFamilyTag(prefab);
                instance.name = $"{namePrefix}{placed:000}_{sourceLabel}_{SanitizeObjectName(prefab.name)}";
                placed++;
            }

            return placed;
        }

        private static bool IsWithinTerrainXZ(Terrain terrain, float worldX, float worldZ)
        {
            if (terrain == null || terrain.terrainData == null)
                return false;

            float minX = terrain.transform.position.x;
            float minZ = terrain.transform.position.z;
            float maxX = minX + terrain.terrainData.size.x;
            float maxZ = minZ + terrain.terrainData.size.z;
            return worldX >= minX && worldX <= maxX && worldZ >= minZ && worldZ <= maxZ;
        }

        private static string ResolveSourceFamilyTag(GameObject prefab)
        {
            if (prefab == null)
                return "Unknown";

            string path = AssetDatabase.GetAssetPath(prefab) ?? string.Empty;
            if (path.IndexOf("UltimateNature", StringComparison.OrdinalIgnoreCase) >= 0)
                return "UltimateNature";
            if (path.IndexOf("UltimateCrops", StringComparison.OrdinalIgnoreCase) >= 0)
                return "UltimateCrops";
            if (path.IndexOf("Environment/Vegetation", StringComparison.OrdinalIgnoreCase) >= 0)
                return "StylizedNatureMegaKit";
            return "Unknown";
        }

        private static string SanitizeObjectName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Asset";

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!(char.IsLetterOrDigit(c) || c == '_'))
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static int ScatterPremadeAcrossTerrain(
            IReadOnlyList<GameObject> candidates,
            string namePrefix,
            int count,
            float minNormHeight,
            float maxNormHeight,
            float maxSteepness,
            float minScale,
            float maxScale,
            int seed,
            float clearZoneRadius,
            ScatterCategory category)
        {
            Terrain terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogWarning($"[EnvironmentGenerator] No terrain found for premade scatter '{namePrefix}'.");
                return 0;
            }

            if (candidates == null || candidates.Count == 0)
                return 0;

            var rng = new System.Random(seed);
            int placed = 0;
            int attempts = 0;
            int maxAttempts = Mathf.Max(6000, count * 80);
            int correctedCount = 0;

            float minX = terrain.transform.position.x;
            float minZ = terrain.transform.position.z;
            float sizeX = terrain.terrainData.size.x;
            float sizeZ = terrain.terrainData.size.z;

            while (placed < count && attempts < maxAttempts)
            {
                attempts++;

                float nx = (float)rng.NextDouble();
                float nz = (float)rng.NextDouble();

                float worldX = minX + (nx * sizeX);
                float worldZ = minZ + (nz * sizeZ);

                if (Mathf.Abs(worldX) < clearZoneRadius && Mathf.Abs(worldZ) < clearZoneRadius)
                    continue;

                float normH = terrain.terrainData.GetInterpolatedHeight(nx, nz) / terrain.terrainData.size.y;
                float steep = terrain.terrainData.GetSteepness(nx, nz);
                if (normH < minNormHeight || normH > maxNormHeight || steep > maxSteepness)
                    continue;

                GameObject prefab = candidates[rng.Next(candidates.Count)];
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (instance == null)
                    continue;

                if (IsNearPathNetwork(terrain, worldX, worldZ, GetPathHalfWidth(category)))
                {
                    Object.DestroyImmediate(instance);
                    continue;
                }

                if (!PassesBiomePlacement(category, terrain, worldX, worldZ, normH, steep, rng))
                {
                    Object.DestroyImmediate(instance);
                    continue;
                }

                float worldY = terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + terrain.transform.position.y;
                float scale = minScale + ((float)rng.NextDouble() * (maxScale - minScale));

                // Capture the prefab's built-in rotation before any overwrite.
                // With bakeAxisConversion=false this contains axis compensation;
                // with bakeAxisConversion=true this is typically identity.
                Quaternion prefabBaseRotation = instance.transform.rotation;

                instance.transform.position = new Vector3(worldX, worldY, worldZ);
                instance.transform.localScale = instance.transform.localScale * scale;

                // Detect orientation from mesh bounds (works in -nographics batchmode
                // unlike Renderer.bounds). Correct if model is still sideways after
                // the prefab's built-in rotation.
                Quaternion uprightCorrection = ComputeUprightCorrectionFromMesh(instance);
                if (uprightCorrection != Quaternion.identity)
                    correctedCount++;

                // Compose: Y-spin * upright-correction * prefab-base
                instance.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f)
                    * uprightCorrection
                    * prefabBaseRotation;

                SnapInstanceToTerrain(terrain, instance, worldX, worldZ, 0.015f);
                instance.name = $"{namePrefix}{placed:000}";
                placed++;
            }

            if (correctedCount > 0)
                Debug.Log($"[EnvironmentGenerator] Upright-corrected {correctedCount}/{placed} objects for '{namePrefix}'.");
            Debug.Log($"[EnvironmentGenerator] Placed {placed}/{count} objects for '{namePrefix}' from {candidates.Count} premade candidates (seed={seed}, attempts={attempts}).");
            return placed;
        }

        private static List<GameObject> LoadPremadeAssetCandidates(string[] roots, string[] includeTokens, string[] excludeTokens)
        {
            var candidatesWithPath = new List<(string Path, GameObject Prefab)>();
            if (roots == null)
                return new List<GameObject>();

            bool includeAll = includeTokens == null || includeTokens.Length == 0;

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                string root = roots[rootIndex];
                if (string.IsNullOrWhiteSpace(root) || !AssetDatabase.IsValidFolder(root))
                    continue;

                string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { root });
                Array.Sort(guids, StringComparer.Ordinal);

                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string name = Path.GetFileNameWithoutExtension(path);
                    if (!includeAll && !ContainsAnyToken(name, includeTokens))
                        continue;

                    if (excludeTokens != null && excludeTokens.Length > 0 && ContainsAnyToken(name, excludeTokens))
                        continue;

                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (!TryHasRenderable(prefab))
                        continue;

                    candidatesWithPath.Add((path, prefab));
                }
            }

            candidatesWithPath.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            return candidatesWithPath.Select(entry => entry.Prefab).ToList();
        }

        private static bool ContainsAnyToken(string value, string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(value) || tokens == null || tokens.Length == 0)
                return false;

            string lower = value.ToLowerInvariant();
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                if (lower.Contains(token.ToLowerInvariant()))
                    return true;
            }

            return false;
        }

        private static float GetPathHalfWidth(ScatterCategory category)
        {
            switch (category)
            {
                case ScatterCategory.Tree:
                    return 4.0f;
                case ScatterCategory.Rock:
                    return 2.8f;
                default:
                    return 1.6f;
            }
        }

        private static bool PassesBiomePlacement(ScatterCategory category, Terrain terrain, float worldX, float worldZ, float normHeight, float steepness, System.Random rng)
        {
            if (terrain == null || terrain.terrainData == null || rng == null)
                return true;

            Vector2 local = WorldToCenteredNormalized(terrain, worldX, worldZ);
            float radial = Mathf.Clamp01(local.magnitude);
            float terrainNoise = Mathf.PerlinNoise((local.x + 1f) * 2.9f + 19.2f, (local.y + 1f) * 2.9f + 53.4f);
            float steepNorm = Mathf.Clamp01(steepness / 55f);

            float meadowWeight = Mathf.Clamp01((1f - radial) * 0.95f + (0.44f - normHeight) * 1.25f + (terrainNoise - 0.5f) * 0.35f);
            float highlandWeight = Mathf.Clamp01((local.y + 0.15f) * 0.95f + (normHeight - 0.40f) * 1.75f + (terrainNoise - 0.5f) * 0.42f + steepNorm * 0.28f);
            float woodlandWeight = Mathf.Clamp01(0.45f + (1f - Mathf.Abs(local.x)) * 0.30f + (terrainNoise - 0.5f) * 0.42f - highlandWeight * 0.22f);

            float acceptance;
            switch (category)
            {
                case ScatterCategory.Tree:
                    acceptance = Mathf.Clamp01(0.26f + woodlandWeight * 0.62f + highlandWeight * 0.26f - meadowWeight * 0.06f);
                    break;
                case ScatterCategory.Rock:
                    acceptance = Mathf.Clamp01(0.16f + highlandWeight * 0.70f + steepNorm * 0.46f - meadowWeight * 0.20f);
                    break;
                default:
                    acceptance = Mathf.Clamp01(0.52f + meadowWeight * 0.52f + woodlandWeight * 0.10f - highlandWeight * 0.22f - steepNorm * 0.20f);
                    break;
            }

            return rng.NextDouble() <= acceptance;
        }

        private static Vector2 WorldToCenteredNormalized(Terrain terrain, float worldX, float worldZ)
        {
            float halfX = terrain.terrainData.size.x * 0.5f;
            float halfZ = terrain.terrainData.size.z * 0.5f;

            float centerX = terrain.transform.position.x + halfX;
            float centerZ = terrain.transform.position.z + halfZ;

            float nx = Mathf.Clamp((worldX - centerX) / Mathf.Max(0.001f, halfX), -1f, 1f);
            float nz = Mathf.Clamp((worldZ - centerZ) / Mathf.Max(0.001f, halfZ), -1f, 1f);
            return new Vector2(nx, nz);
        }

        private static bool IsNearPathNetwork(Terrain terrain, float worldX, float worldZ, float halfWidth)
        {
            if (terrain == null || terrain.terrainData == null || halfWidth <= 0f)
                return false;

            Vector2 point = new Vector2(worldX, worldZ);
            float minDistance = float.MaxValue;

            minDistance = Mathf.Min(minDistance, DistanceToPathChain(terrain, point, MainPathPointsNormalized));
            minDistance = Mathf.Min(minDistance, DistanceToPathChain(terrain, point, BranchPathPointsNormalizedA));
            minDistance = Mathf.Min(minDistance, DistanceToPathChain(terrain, point, BranchPathPointsNormalizedB));

            float widthNoise = Mathf.PerlinNoise(worldX * 0.013f + 7.2f, worldZ * 0.013f + 5.6f);
            float effectiveHalfWidth = halfWidth * Mathf.Lerp(0.85f, 1.20f, widthNoise);
            return minDistance <= effectiveHalfWidth;
        }

        private static float DistanceToPathChain(Terrain terrain, Vector2 worldPoint, Vector2[] normalizedPoints)
        {
            if (terrain == null || normalizedPoints == null || normalizedPoints.Length < 2)
                return float.MaxValue;

            float minDistance = float.MaxValue;
            for (int i = 1; i < normalizedPoints.Length; i++)
            {
                Vector2 a = NormalizedToWorld(terrain, normalizedPoints[i - 1]);
                Vector2 b = NormalizedToWorld(terrain, normalizedPoints[i]);
                float distance = DistancePointToSegment(worldPoint, a, b);
                if (distance < minDistance)
                    minDistance = distance;
            }

            return minDistance;
        }

        private static Vector2 NormalizedToWorld(Terrain terrain, Vector2 normalized)
        {
            float halfX = terrain.terrainData.size.x * 0.5f;
            float halfZ = terrain.terrainData.size.z * 0.5f;

            float centerX = terrain.transform.position.x + halfX;
            float centerZ = terrain.transform.position.z + halfZ;
            return new Vector2(centerX + (normalized.x * halfX), centerZ + (normalized.y * halfZ));
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 segmentA, Vector2 segmentB)
        {
            Vector2 ab = segmentB - segmentA;
            float abSqr = ab.sqrMagnitude;
            if (abSqr <= 0.0001f)
                return Vector2.Distance(point, segmentA);

            float t = Mathf.Clamp01(Vector2.Dot(point - segmentA, ab) / abSqr);
            Vector2 closest = segmentA + (ab * t);
            return Vector2.Distance(point, closest);
        }

        private static bool TryHasRenderable(GameObject prefab)
        {
            if (prefab == null)
                return false;

            // First check MeshFilter (always available, even in -nographics)
            var meshFilters = prefab.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            if (meshFilters != null)
            {
                for (int i = 0; i < meshFilters.Length; i++)
                {
                    if (meshFilters[i] != null && meshFilters[i].sharedMesh != null
                        && meshFilters[i].sharedMesh.bounds.size.sqrMagnitude > 0.0001f)
                        return true;
                }
            }

            // Fallback to Renderer bounds (may fail in -nographics)
            var renderers = prefab.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null || renderers.Length == 0)
                return false;

            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (renderer.bounds.size.sqrMagnitude > 0.0001f)
                    return true;
            }

            return false;
        }

        private static int ScatterLargeTreesAcrossTerrain(int count, int seed)
        {
            Terrain terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogWarning("[EnvironmentGenerator] No terrain found for world tree scatter.");
                return 0;
            }

            List<GameObject> variationPrefabs = new List<GameObject>();
            for (int i = 0; i < LargeTreeGrowthStages.Length; i++)
            {
                string stage = LargeTreeGrowthStages[i];
                string path = $"{LargeTreeVariationPrefabRoot}/{stage}/LargeTree_{stage}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    variationPrefabs.Add(prefab);
            }

            if (variationPrefabs.Count == 0)
            {
                Debug.LogWarning("[EnvironmentGenerator] No LargeTree variation prefabs found for world tree scatter.");
                return 0;
            }

            var rng = new System.Random(seed);
            int placed = 0;
            int attempts = 0;
            int maxAttempts = Mathf.Max(5000, count * 80);

            float minX = terrain.transform.position.x;
            float minZ = terrain.transform.position.z;
            float sizeX = terrain.terrainData.size.x;
            float sizeZ = terrain.terrainData.size.z;

            while (placed < count && attempts < maxAttempts)
            {
                attempts++;
                float nx = (float)rng.NextDouble();
                float nz = (float)rng.NextDouble();

                float worldX = minX + (nx * sizeX);
                float worldZ = minZ + (nz * sizeZ);

                // Keep a smaller center clear zone so the map feels populated immediately.
                if (Mathf.Abs(worldX) < 14f && Mathf.Abs(worldZ) < 14f)
                    continue;

                float normH = terrain.terrainData.GetInterpolatedHeight(nx, nz) / terrain.terrainData.size.y;
                float steep = terrain.terrainData.GetSteepness(nx, nz);
                if (normH < 0.035f || normH > 0.70f || steep > 34f)
                    continue;

                GameObject prefab = variationPrefabs[SelectTreeVariationIndex(rng, variationPrefabs.Count)];
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (instance == null)
                    continue;

                float worldY = terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + terrain.transform.position.y;
                float jitter = 0.92f + ((float)rng.NextDouble() * 0.24f);

                instance.transform.position = new Vector3(worldX, worldY, worldZ);
                instance.transform.localScale = instance.transform.localScale * jitter;
                instance.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                SnapInstanceToTerrain(terrain, instance, worldX, worldZ, 0.02f);
                instance.name = $"Placed_WorldTree_{placed:000}";
                placed++;
            }

            Debug.Log($"[EnvironmentGenerator] Placed {placed}/{count} world trees (seed={seed}, attempts={attempts}).");
            return placed;
        }

        private static int SelectTreeVariationIndex(System.Random rng, int variantCount)
        {
            if (variantCount <= 1)
                return 0;

            // Weighted toward mid/late growth stages for better long-range silhouette coverage.
            float[] weights = { 0.08f, 0.18f, 0.32f, 0.27f, 0.15f };
            float total = 0f;
            int available = Mathf.Min(variantCount, weights.Length);
            for (int i = 0; i < available; i++)
                total += weights[i];

            float roll = (float)rng.NextDouble() * total;
            float cumulative = 0f;
            for (int i = 0; i < available; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                    return i;
            }

            return available - 1;
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
                    UnityEngine.Random.Range(0f, 360f),
                    UnityEngine.Random.Range(0f, 360f),
                    UnityEngine.Random.Range(0f, 360f));
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
            const int MaxAttempts = 7000;

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
                    worldX = terrain.transform.position.x + (nx * terrain.terrainData.size.x);
                    worldZ = terrain.transform.position.z + (nz * terrain.terrainData.size.z);

                    float normH = terrain.terrainData.GetInterpolatedHeight(nx, nz) / terrain.terrainData.size.y;
                    float steep = terrain.terrainData.GetSteepness(nx, nz);
                    if (normH < minNormHeight || normH > maxNormHeight || steep > maxSteepness)
                        continue;

                    // Keep a smaller clear zone around spawn so content appears close by.
                    if (Mathf.Abs(worldX) < 14f && Mathf.Abs(worldZ) < 14f) continue;

                    worldY = terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + terrain.transform.position.y;
                }

                float scale = (float)(minScale + rng.NextDouble() * (maxScale - minScale));
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = new Vector3(worldX, worldY, worldZ);
                go.transform.localScale = Vector3.one * scale;
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                if (terrain != null)
                    SnapInstanceToTerrain(terrain, go, worldX, worldZ, 0.015f);

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

            // If the material already exists with a valid Standard shader, reuse it.
            // Prevents -nographics builds from overwriting good materials with fallback.
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && existing.shader != null && existing.shader.name == "Standard")
            {
                existing.color = color;
                if (existing.HasProperty("_Metallic")) existing.SetFloat("_Metallic", metallic);
                if (existing.HasProperty("_Glossiness")) existing.SetFloat("_Glossiness", smoothness);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            if (existing != null)
                AssetDatabase.DeleteAsset(path);

            // Use Standard shader (BIRP) directly — URP/Lit resolves to a pink stub in BIRP builds.
            // In batchmode with -nographics, Shader.Find can return null. Try fallback.
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Diffuse");
                if (shader == null)
                    Debug.LogError("[BuildGen] EnvironmentGenerator: No suitable shader found! Materials will be pink. Avoid -nographics.");
                else
                    Debug.LogWarning("[BuildGen] EnvironmentGenerator: 'Standard' shader not found, using 'Diffuse' fallback.");
            }
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

        private static void CleanupLegacyEnvironmentPlacements()
        {
            string[] prefixesToDelete =
            {
                "RockCluster",
                "Boulder",
                "FallenLog",
                "Placed_BiomeTree_",
                "Placed_LargeTree_",
                "Placed_WorldTree_",
                "Placed_QuaterniusTree_",
                "Placed_QuaterniusVegetation_",
                "Placed_QuaterniusRock_",
                "Showcase_QuaterniusTree_",
                "Showcase_QuaterniusVegetation_",
                "Showcase_QuaterniusRock_",
            };

            int removed = 0;
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null || t.parent != null)
                    continue;

                if (!t.gameObject.scene.IsValid())
                    continue;

                string name = t.name;
                bool matches = false;
                for (int j = 0; j < prefixesToDelete.Length; j++)
                {
                    if (name.StartsWith(prefixesToDelete[j], StringComparison.Ordinal))
                    {
                        matches = true;
                        break;
                    }
                }

                if (!matches)
                    continue;

                Object.DestroyImmediate(t.gameObject);
                removed++;
            }

            if (removed > 0)
                Debug.Log($"[EnvironmentGenerator] Removed {removed} legacy/previous environment placements before rescatters.");
        }

        private static void DeleteLegacyEnvironmentPrefabs()
        {
            DeleteAssetIfExists(RockPrefabPath);
            DeleteAssetIfExists(BoulderPrefabPath);
            DeleteAssetIfExists(LogPrefabPath);
        }

        private static void DeleteAssetIfExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
        }

        private static Vector3 ComputeRenderableBoundsSize(GameObject root)
        {
            if (root == null)
                return Vector3.zero;

            if (!TryGetRenderableBounds(root, out Bounds bounds))
                return Vector3.zero;

            return bounds.size;
        }

        /// <summary>
        /// Computes the upright correction rotation for a model by examining its
        /// MeshFilter bounds. Unlike Renderer.bounds, MeshFilter.sharedMesh.bounds
        /// is always available even in -nographics batchmode because it reads
        /// serialized mesh data directly.
        /// </summary>
        private static Quaternion ComputeUprightCorrectionFromMesh(GameObject root)
        {
            if (root == null)
                return Quaternion.identity;

            Vector3 size = ComputeMeshBoundsWorldSize(root);
            return ResolvePremadeUprightRotation(size);
        }

        /// <summary>
        /// Computes world-space AABB size using MeshFilter.sharedMesh.bounds,
        /// which works reliably in -nographics batchmode (unlike Renderer.bounds).
        /// </summary>
        private static Vector3 ComputeMeshBoundsWorldSize(GameObject root)
        {
            if (root == null)
                return Vector3.zero;

            var meshFilters = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            if (meshFilters == null || meshFilters.Length == 0)
                return Vector3.zero;

            Bounds combined = default;
            bool hasBounds = false;

            for (int fi = 0; fi < meshFilters.Length; fi++)
            {
                var mf = meshFilters[fi];
                if (mf == null || mf.sharedMesh == null)
                    continue;

                Bounds mb = mf.sharedMesh.bounds;
                if (mb.size.sqrMagnitude <= 0.0001f)
                    continue;

                // Transform the 8 corners of the mesh-local AABB to world space
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                Vector3 center = mb.center;
                Vector3 ext = mb.extents;

                for (int cx = -1; cx <= 1; cx += 2)
                {
                    for (int cy = -1; cy <= 1; cy += 2)
                    {
                        for (int cz = -1; cz <= 1; cz += 2)
                        {
                            Vector3 corner = m.MultiplyPoint3x4(
                                center + new Vector3(ext.x * cx, ext.y * cy, ext.z * cz));

                            if (!hasBounds)
                            {
                                combined = new Bounds(corner, Vector3.zero);
                                hasBounds = true;
                            }
                            else
                            {
                                combined.Encapsulate(corner);
                            }
                        }
                    }
                }
            }

            return hasBounds ? combined.size : Vector3.zero;
        }

        private static Quaternion ResolvePremadeUprightRotation(Vector3 renderableSize)
        {
            float sx = Mathf.Abs(renderableSize.x);
            float sy = Mathf.Abs(renderableSize.y);
            float sz = Mathf.Abs(renderableSize.z);

            float horizontalMax = Mathf.Max(sx, sz);
            if (horizontalMax <= 0.001f)
                return Quaternion.identity;

            if (sy >= horizontalMax * 0.55f)
                return Quaternion.identity;

            if (sz >= sx)
                return Quaternion.Euler(-90f, 0f, 0f);

            return Quaternion.Euler(0f, 0f, 90f);
        }

        private static void SnapInstanceToTerrain(Terrain terrain, GameObject instance, float worldX, float worldZ, float clearance)
        {
            if (terrain == null || terrain.terrainData == null || instance == null)
                return;

            float terrainY = terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + terrain.transform.position.y;
            Vector3 pos = instance.transform.position;
            pos.x = worldX;
            pos.y = terrainY;
            pos.z = worldZ;
            instance.transform.position = pos;

            if (TryGetRenderableBounds(instance, out Bounds bounds))
            {
                float offset = (terrainY + clearance) - bounds.min.y;
                instance.transform.position += Vector3.up * offset;
                return;
            }

            // Fallback: use mesh-filter bounds when renderer bounds are unavailable
            // (common in -nographics batchmode).
            if (TryGetMeshBounds(instance, out Bounds meshBounds))
            {
                float offset = (terrainY + clearance) - meshBounds.min.y;
                instance.transform.position += Vector3.up * offset;
                return;
            }

            var rootCollider = instance.GetComponent<Collider>();
            if (rootCollider != null)
            {
                float offset = (terrainY + clearance) - rootCollider.bounds.min.y;
                instance.transform.position += Vector3.up * offset;
            }
        }

        private static bool TryGetMeshBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
                return false;

            Vector3 size = ComputeMeshBoundsWorldSize(root);
            if (size.sqrMagnitude <= 0.0001f)
                return false;

            // Reconstruct the world AABB from mesh filters
            var meshFilters = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            bool hasBounds = false;
            for (int fi = 0; fi < meshFilters.Length; fi++)
            {
                var mf = meshFilters[fi];
                if (mf == null || mf.sharedMesh == null)
                    continue;

                Bounds mb = mf.sharedMesh.bounds;
                if (mb.size.sqrMagnitude <= 0.0001f)
                    continue;

                Matrix4x4 m = mf.transform.localToWorldMatrix;
                Vector3 center = mb.center;
                Vector3 ext = mb.extents;

                for (int cx = -1; cx <= 1; cx += 2)
                {
                    for (int cy = -1; cy <= 1; cy += 2)
                    {
                        for (int cz = -1; cz <= 1; cz += 2)
                        {
                            Vector3 corner = m.MultiplyPoint3x4(
                                center + new Vector3(ext.x * cx, ext.y * cy, ext.z * cz));

                            if (!hasBounds)
                            {
                                bounds = new Bounds(corner, Vector3.zero);
                                hasBounds = true;
                            }
                            else
                            {
                                bounds.Encapsulate(corner);
                            }
                        }
                    }
                }
            }

            return hasBounds;
        }

        private static bool TryGetRenderableBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
                return false;

            bool hasBounds = false;
            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.bounds.size.sqrMagnitude <= 0.000001f)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds;
        }
    }
}
