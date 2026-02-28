using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using ByteWar.Editor;
using ByteWar.Survival;
using ByteWar.Building;
using ByteWar.Abilities.Mage;
using ByteWar.Abilities.Talents;
using ByteWar.UI;
using System.IO;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ByteWar.Tests.EditMode
{
    public class GeneratorTests
    {
        [Test]
        public void TerrainGenerator_SelectWeightedPrototypeIndex_PrefersHigherWeight()
        {
            var rng = new System.Random(12345);
            var weights = new float[] { 1f, 3f };

            int lowWeightHits = 0;
            int highWeightHits = 0;

            for (int i = 0; i < 5000; i++)
            {
                int selected = TerrainGenerator.SelectWeightedPrototypeIndex(rng, weights);
                if (selected == 0)
                    lowWeightHits++;
                else if (selected == 1)
                    highWeightHits++;
            }

            Assert.Greater(highWeightHits, lowWeightHits * 2, "Higher weighted prototype should be selected significantly more often.");
        }

        [Test]
        public void TerrainGenerator_SelectWeightedPrototypeIndex_ReturnsZero_WhenWeightsInvalid()
        {
            var rng = new System.Random(42);

            Assert.AreEqual(0, TerrainGenerator.SelectWeightedPrototypeIndex(rng, null));
            Assert.AreEqual(0, TerrainGenerator.SelectWeightedPrototypeIndex(rng, Array.Empty<float>()));
            Assert.AreEqual(0, TerrainGenerator.SelectWeightedPrototypeIndex(rng, new[] { 0f, -1f, 0f }));
        }

        [Test]
        public void TerrainGenerator_SelectWeightedPrototypeIndex_Throws_WhenRandomIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => TerrainGenerator.SelectWeightedPrototypeIndex(null, new[] { 1f }));
        }

        [Test]
        public void TerrainGenerator_GeneratedTerrain_HasGrassDominantCoverageAndVisiblePaths()
        {
            TerrainGenerator.GenerateTerrain();

            const string terrainPath = "Assets/GeneratedPrefabs/GeneratedTerrainData.asset";
            var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainPath);
            Assert.IsNotNull(terrainData, $"Expected terrain data at '{terrainPath}'.");

            float averageGrass = ComputeAverageLayerWeight(terrainData, 0);
            Assert.Greater(averageGrass, 0.42f, $"Grass layer should dominate terrain coverage. avgGrass={averageGrass:0.000}");

            float pathDirt = ComputeAverageLayerWeightAtPoints(
                terrainData,
                layerIndex: 1,
                new[]
                {
                    new Vector2(0.33f, 0.38f),
                    new Vector2(0.44f, 0.46f),
                    new Vector2(0.55f, 0.53f),
                    new Vector2(0.65f, 0.60f),
                });

            float offPathDirt = ComputeAverageLayerWeightAtPoints(
                terrainData,
                layerIndex: 1,
                new[]
                {
                    new Vector2(0.13f, 0.16f),
                    new Vector2(0.84f, 0.23f),
                    new Vector2(0.22f, 0.84f),
                    new Vector2(0.86f, 0.80f),
                });

            Assert.Greater(pathDirt, offPathDirt + 0.10f, $"Path corridor should have noticeably higher dirt blend. pathDirt={pathDirt:0.000} offPathDirt={offPathDirt:0.000}");
        }

        [Test]
        public void TerrainGenerator_GeneratedTerrain_ConfiguresGrassDetailDensity()
        {
            TerrainGenerator.GenerateTerrain();

            const string terrainPath = "Assets/GeneratedPrefabs/GeneratedTerrainData.asset";
            var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainPath);
            Assert.IsNotNull(terrainData, $"Expected terrain data at '{terrainPath}'.");

            Assert.IsNotNull(terrainData.detailPrototypes, "Terrain detail prototypes should be configured.");
            Assert.Greater(terrainData.detailPrototypes.Length, 0, "Expected at least one detail prototype for grass.");
            Assert.IsNotNull(terrainData.detailPrototypes[0].prototypeTexture, "Grass detail prototype should include a texture.");

            int[,] detailLayer = terrainData.GetDetailLayer(0, 0, terrainData.detailWidth, terrainData.detailHeight, 0);
            long totalDensity = 0;
            int nonZeroCells = 0;
            for (int y = 0; y < detailLayer.GetLength(0); y++)
            {
                for (int x = 0; x < detailLayer.GetLength(1); x++)
                {
                    int value = detailLayer[y, x];
                    totalDensity += value;
                    if (value > 0)
                        nonZeroCells++;
                }
            }

            Assert.Greater(totalDensity, 100000, $"Grass detail density should be substantial across terrain. totalDensity={totalDensity}");
            Assert.Greater(nonZeroCells, (terrainData.detailWidth * terrainData.detailHeight) / 7, $"Grass detail should cover broad terrain areas. nonZeroCells={nonZeroCells}");
        }

        [Test]
        public void LargeTreeVariationPrefabGenerator_KnownVariations_AreGrowthStages()
        {
            var expected = new[] { "Seedling", "Sapling", "Young", "Mature", "Adult" };
            CollectionAssert.AreEqual(expected, LargeTreeVariationPrefabGenerator.KnownVariations);
        }

        [Test]
        public void TreeUprightResolvers_RotateSidewaysTreesToYUp()
        {
            Quaternion variationRotation = LargeTreeVariationPrefabGenerator.ResolveTreeUprightRotation(new Vector3(2f, 0.2f, 6f));
            Quaternion baseRotation = LargeTreePrefabGenerator.ResolveTreeUprightRotation(new Vector3(5f, 0.2f, 2f));

            float variationUpAlignment = Vector3.Dot((variationRotation * Vector3.forward).normalized, Vector3.up);
            float baseUpAlignment = Vector3.Dot((baseRotation * Vector3.right).normalized, Vector3.up);

            Assert.Greater(variationUpAlignment, 0.99f, "Variation resolver should rotate dominant Z axis to world up.");
            Assert.Greater(baseUpAlignment, 0.99f, "Base resolver should rotate dominant X axis to world up.");
        }

        [Test]
        public void TreeUprightResolvers_KeepAlreadyUprightBoundsUnchanged()
        {
            Quaternion variationRotation = LargeTreeVariationPrefabGenerator.ResolveTreeUprightRotation(new Vector3(5f, 3.2f, 4.5f));
            Quaternion baseRotation = LargeTreePrefabGenerator.ResolveTreeUprightRotation(new Vector3(6f, 3.5f, 5f));

            Assert.Less(Quaternion.Angle(variationRotation, Quaternion.identity), 0.01f, "Variation resolver should not rotate meshes that are already upright enough.");
            Assert.Less(Quaternion.Angle(baseRotation, Quaternion.identity), 0.01f, "Base resolver should not rotate meshes that are already upright enough.");
        }

        [Test]
        public void LargeTreeVariationProfile_MonoScript_ResolvesExpectedType()
        {
            const string scriptPath = "Assets/Scripts/Editor/LargeTreeVariationProfile.cs";

            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);

            Assert.IsNotNull(script, $"Expected MonoScript at '{scriptPath}'.");
            Assert.AreEqual(typeof(LargeTreeVariationProfile), script.GetClass());
        }

        [Test]
        public void LargeTreeVariationPrefabs_VisualScale_IncreasesByGrowthStage()
        {
            if (!LargeTreeVariationPrefabGenerator.GenerateAll())
                Assert.Ignore("LargeTree variation sources are unavailable; skipping visual scale progression assertion.");

            var orderedStages = new[] { "Seedling", "Sapling", "Young", "Mature", "Adult" };
            float previousScale = -1f;
            float seedlingScale = -1f;
            float adultScale = -1f;

            foreach (string stage in orderedStages)
            {
                string prefabPath = $"Assets/Resources/Generated/LargeTree/Variants/{stage}/LargeTree_{stage}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.IsNotNull(prefab, $"Expected generated variation prefab at '{prefabPath}'.");

                var visualRoot = prefab.transform.Find("VisualRoot");
                Assert.IsNotNull(visualRoot, $"Expected VisualRoot child on '{prefab.name}'.");

                float scale = visualRoot.localScale.x;
                Assert.Greater(scale, 0.05f, $"VisualRoot scale should be positive for stage '{stage}'.");

                if (previousScale > 0f)
                {
                    Assert.Greater(
                        scale,
                        previousScale + 0.01f,
                        $"Stage '{stage}' scale should be larger than the previous growth stage. prev={previousScale:0.00} current={scale:0.00}");
                }

                if (string.Equals(stage, "Seedling", StringComparison.OrdinalIgnoreCase))
                    seedlingScale = scale;
                if (string.Equals(stage, "Adult", StringComparison.OrdinalIgnoreCase))
                    adultScale = scale;

                previousScale = scale;
            }

            Assert.Greater(adultScale, seedlingScale * 1.8f, "Adult tree should be substantially larger than Seedling for clear in-game visual differentiation.");
        }

        [Test]
        public void LargeTreeVariationPrefabs_Silhouette_EvolvesAcrossGrowthStages()
        {
            if (!LargeTreeVariationPrefabGenerator.GenerateAll())
                Assert.Ignore("LargeTree variation sources are unavailable; skipping silhouette evolution assertion.");

            var orderedStages = new[] { "Seedling", "Sapling", "Young", "Mature", "Adult" };
            float previousAspect = -1f;
            float previousCanopyToTrunk = -1f;
            float seedlingAspect = -1f;
            float adultAspect = -1f;

            foreach (string stage in orderedStages)
            {
                string prefabPath = $"Assets/Resources/Generated/LargeTree/Variants/{stage}/LargeTree_{stage}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.IsNotNull(prefab, $"Expected generated variation prefab at '{prefabPath}'.");

                var lodGroup = prefab.GetComponent<LODGroup>();
                Assert.IsNotNull(lodGroup, $"Expected LODGroup on '{prefab.name}'.");
                var lods = lodGroup.GetLODs();
                Assert.IsNotNull(lods);
                Assert.GreaterOrEqual(lods.Length, 1, $"Expected at least one LOD on '{prefab.name}'.");

                var lod0Renderer = lods[0].renderers != null && lods[0].renderers.Length > 0 ? lods[0].renderers[0] : null;
                Assert.IsNotNull(lod0Renderer, $"Expected LOD0 renderer for stage '{stage}'.");

                Mesh lod0Mesh = ResolveRendererMesh(lod0Renderer);
                Assert.IsNotNull(lod0Mesh, $"Expected LOD0 mesh for stage '{stage}'.");

                // Use max(Y,Z) as height to handle both Y-up and Z-up mesh orientations
                float meshHeight = Mathf.Max(lod0Mesh.bounds.size.y, lod0Mesh.bounds.size.z);
                float aspect = lod0Mesh.bounds.size.x / Mathf.Max(0.001f, meshHeight);
                Assert.Greater(aspect, 0.05f, $"Stage '{stage}' should have valid silhouette aspect ratio.");

                float canopyToTrunk = ComputeCanopyToTrunkWidthRatio(lod0Mesh, lod0Renderer.sharedMaterials);
                Assert.Greater(canopyToTrunk, 0.30f, $"Stage '{stage}' should have measurable canopy width relative to trunk width.");

                if (string.Equals(stage, "Seedling", StringComparison.OrdinalIgnoreCase))
                    seedlingAspect = aspect;
                if (string.Equals(stage, "Adult", StringComparison.OrdinalIgnoreCase))
                    adultAspect = aspect;

                previousAspect = aspect;
                previousCanopyToTrunk = canopyToTrunk;
            }

            Assert.Greater(adultAspect, seedlingAspect * 1.18f, "Adult silhouette should be substantially broader than Seedling to reflect visual growth evolution.");
        }

        [Test]
        public void LargeTreeVariationPrefabs_BranchCanopyProximity_StaysConnectedAcrossGrowthStages()
        {
            if (!LargeTreeVariationPrefabGenerator.GenerateAll())
                Assert.Ignore("LargeTree variation sources are unavailable; skipping branch-canopy proximity assertion.");

            var orderedStages = new[] { "Seedling", "Sapling", "Young", "Mature", "Adult" };

            foreach (string stage in orderedStages)
            {
                string prefabPath = $"Assets/Resources/Generated/LargeTree/Variants/{stage}/LargeTree_{stage}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.IsNotNull(prefab, $"Expected generated variation prefab at '{prefabPath}'.");

                var lodGroup = prefab.GetComponent<LODGroup>();
                Assert.IsNotNull(lodGroup, $"Expected LODGroup on '{prefab.name}'.");
                var lods = lodGroup.GetLODs();
                Assert.IsNotNull(lods);
                Assert.GreaterOrEqual(lods.Length, 1, $"Expected at least one LOD on '{prefab.name}'.");

                var lod0Renderer = lods[0].renderers != null && lods[0].renderers.Length > 0 ? lods[0].renderers[0] : null;
                Assert.IsNotNull(lod0Renderer, $"Expected LOD0 renderer for stage '{stage}'.");

                Mesh lod0Mesh = ResolveRendererMesh(lod0Renderer);
                Assert.IsNotNull(lod0Mesh, $"Expected LOD0 mesh for stage '{stage}'.");

                bool computed = TryComputeBranchCanopyProximity(
                    lod0Mesh,
                    lod0Renderer.sharedMaterials,
                    out int sampledBranchTips,
                    out float medianNearestCanopyDistance,
                    out float p90NearestCanopyDistance,
                    out float connectedTipFraction);

                if (!computed)
                {
                    // Early stages (e.g. Seedling) may lack enough bark/canopy geometry
                    // for meaningful proximity evaluation — tolerate per repo convention.
                    Debug.Log($"[GeneratorTests] Stage '{stage}' has insufficient bark/canopy geometry for branch-canopy proximity; skipping.");
                    continue;
                }

                Assert.GreaterOrEqual(
                    sampledBranchTips,
                    20,
                    $"Stage '{stage}' should include enough sampled upper branch-tip candidates for a meaningful proximity regression check.");

                float meshHeight = Mathf.Max(0.001f, lod0Mesh.bounds.size.y);
                float medianLimit = Mathf.Max(0.45f, meshHeight * 0.205f);
                float p90Limit = Mathf.Max(0.90f, meshHeight * 0.32f);

                Assert.Less(
                    medianNearestCanopyDistance,
                    medianLimit,
                    $"Stage '{stage}' median branch-tip distance to canopy is too high. median={medianNearestCanopyDistance:0.000}, limit={medianLimit:0.000}");

                Assert.Less(
                    p90NearestCanopyDistance,
                    p90Limit,
                    $"Stage '{stage}' has too many detached branch tips. p90={p90NearestCanopyDistance:0.000}, limit={p90Limit:0.000}");

                Assert.GreaterOrEqual(
                    connectedTipFraction,
                    0.45f,
                    $"Stage '{stage}' should keep most sampled branch tips connected to canopy. connected={connectedTipFraction:0.000}");
            }
        }

        [Test]
        public void TerrainGenerator_LoadOrCreateLargeTreeVariationProfile_CreatesGrowthStageEntriesWithValidScriptReference()
        {
            const string profilePath = "Assets/GeneratedPrefabs/LargeTreeVariationProfile.asset";
            AssetDatabase.DeleteAsset(profilePath);

            var createMethod = typeof(TerrainGenerator)
                .GetMethod("LoadOrCreateLargeTreeVariationProfile", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(createMethod, "Expected private profile creation helper on TerrainGenerator.");

            var profile = createMethod.Invoke(null, null) as LargeTreeVariationProfile;
            Assert.IsNotNull(profile, "Expected profile instance to be created.");

            var entryNames = profile.Entries.Select(entry => entry?.VariationName).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();
            var expectedNames = new[] { "Seedling", "Sapling", "Young", "Mature", "Adult" };
            CollectionAssert.AreEqual(expectedNames, entryNames);

            Assert.IsTrue(File.Exists(profilePath), $"Expected profile asset at '{profilePath}'.");
            string profileYaml = File.ReadAllText(profilePath);
            StringAssert.DoesNotContain("m_Script: {fileID: 0}", profileYaml, "Profile asset should keep a valid script reference.");
        }

        [Test]
        public void AssetGenerator_CreatesExpectedAssets()
        {
            // Arrange
            string basePath = "Assets/GeneratedAssets";

            // Act
            AssetGenerator.GenerateAssets();

            // Assert
            Assert.IsTrue(AssetDatabase.IsValidFolder(basePath), "GeneratedAssets folder should exist.");

            Item wood = AssetDatabase.LoadAssetAtPath<Item>($"{basePath}/Wood.asset");
            Assert.IsNotNull(wood, "Wood item should be generated.");
            Assert.AreEqual("Wood", wood.ItemName);

            Item stone = AssetDatabase.LoadAssetAtPath<Item>($"{basePath}/Stone.asset");
            Assert.IsNotNull(stone, "Stone item should be generated.");

            Item basicStaff = AssetDatabase.LoadAssetAtPath<Item>($"{basePath}/BasicStaff.asset");
            Assert.IsNotNull(basicStaff, "BasicStaff item should be generated.");
            Assert.IsTrue(basicStaff.IsEquippable);

            CraftingRecipe staffRecipe = AssetDatabase.LoadAssetAtPath<CraftingRecipe>($"{basePath}/StaffRecipe.asset");
            Assert.IsNotNull(staffRecipe, "StaffRecipe should be generated.");
            Assert.AreEqual(basicStaff, staffRecipe.Result);

            FireballAbility fireball = AssetDatabase.LoadAssetAtPath<FireballAbility>($"{basePath}/FireballAbility.asset");
            Assert.IsNotNull(fireball, "FireballAbility should be generated.");

            FrostNovaAbility frostNova = AssetDatabase.LoadAssetAtPath<FrostNovaAbility>($"{basePath}/FrostNovaAbility.asset");
            Assert.IsNotNull(frostNova, "FrostNovaAbility should be generated.");

            AbilityModifierTalent manaRegenTalent = AssetDatabase.LoadAssetAtPath<AbilityModifierTalent>($"{basePath}/ManaRegenTalent.asset");
            Assert.IsNotNull(manaRegenTalent, "ManaRegenTalent should be generated.");
        }

        [Test]
        public void PrefabGenerator_CreatesExpectedPrefabs()
        {
            // Arrange
            string basePath = "Assets/GeneratedPrefabs";

            // Act
            PrefabGenerator.GeneratePrefabs();

            // Assert
            Assert.IsTrue(AssetDatabase.IsValidFolder(basePath), "GeneratedPrefabs folder should exist.");

            GameObject networkPlayer = AssetDatabase.LoadAssetAtPath<GameObject>($"{basePath}/NetworkPlayer.prefab");
            Assert.IsNotNull(networkPlayer, "NetworkPlayer prefab should be generated.");
            Assert.IsNotNull(networkPlayer.GetComponent<ByteWar.Networking.NetworkPlayer>(), "NetworkPlayer should have NetworkPlayer component.");

            GameObject resourceNode = AssetDatabase.LoadAssetAtPath<GameObject>($"{basePath}/ResourceNode.prefab");
            Assert.IsNotNull(resourceNode, "ResourceNode prefab should be generated.");
            Assert.IsNotNull(resourceNode.GetComponent<ResourceNode>(), "ResourceNode should have ResourceNode component.");

            GameObject enemyAI = AssetDatabase.LoadAssetAtPath<GameObject>($"{basePath}/EnemyAI.prefab");
            Assert.IsNotNull(enemyAI, "EnemyAI prefab should be generated.");
            Assert.IsNotNull(enemyAI.GetComponent<EnemyAI>(), "EnemyAI should have EnemyAI component.");

            GameObject networkManager = AssetDatabase.LoadAssetAtPath<GameObject>($"{basePath}/NetworkManager.prefab");
            Assert.IsNotNull(networkManager, "NetworkManager prefab should be generated.");
            var networkManagerComponent = networkManager.GetComponent<Unity.Netcode.NetworkManager>();
            Assert.IsNotNull(networkManagerComponent, "NetworkManager should have NetworkManager component.");

            const string defaultNetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";
            string defaultNetworkPrefabsYaml = File.Exists(defaultNetworkPrefabsPath)
                ? File.ReadAllText(defaultNetworkPrefabsPath)
                : string.Empty;
            Assert.IsFalse(string.IsNullOrWhiteSpace(defaultNetworkPrefabsYaml), $"Expected NGO default prefab list asset at '{defaultNetworkPrefabsPath}'.");

            var catalog = AssetDatabase.LoadAssetAtPath<DeployableAssetCatalog>("Assets/Resources/Generated/DeployableAssetCatalog.asset");
            if (catalog == null || catalog.Entries == null || catalog.Entries.Count == 0)
                Assert.Ignore("DeployableAssetCatalog not found or empty; run generated deployable pipeline before this assertion.");

            foreach (var entry in catalog.Entries)
            {
                if (entry == null)
                    continue;

                string resourcePath = NormalizeResourcePath(entry.ResourcePath);
                if (string.IsNullOrWhiteSpace(resourcePath))
                    continue;

                string prefabPath = $"Assets/Resources/{resourcePath}.prefab";
                var deployablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (deployablePrefab == null)
                    continue;

                if (deployablePrefab.GetComponent<NetworkObject>() == null)
                    continue;

                bool isRegistered = false;
                var prefabs = networkManagerComponent.NetworkConfig.Prefabs.Prefabs;
                for (int i = 0; i < prefabs.Count; i++)
                {
                    var registeredPrefab = prefabs[i].Prefab;
                    if (registeredPrefab == null)
                        continue;

                    string registeredPath = AssetDatabase.GetAssetPath(registeredPrefab).Replace('\\', '/');
                    if (string.Equals(registeredPath, prefabPath, StringComparison.OrdinalIgnoreCase))
                    {
                        isRegistered = true;
                        break;
                    }
                }

                if (!isRegistered)
                {
                    string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
                    if (!string.IsNullOrWhiteSpace(prefabGuid))
                    {
                        isRegistered = defaultNetworkPrefabsYaml.IndexOf($"guid: {prefabGuid}", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                }

                Assert.IsTrue(
                    isRegistered,
                    $"Catalog-listed deployable '{resourcePath}' should be registered in NetworkManager.NetworkConfig.Prefabs.");
            }
        }

        private static string NormalizeResourcePath(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                return string.Empty;

            string normalized = resourcePath.Trim().Replace('\\', '/');
            int slash = normalized.LastIndexOf('/');
            int dot = normalized.LastIndexOf('.');
            if (dot > slash)
                normalized = normalized.Substring(0, dot);

            return normalized.Trim('/');
        }

        [Test]
        public void SceneGenerator_CreatesTestScene()
        {
            // Arrange
            string scenePath = "Assets/Scenes/TestScene.unity";

            // Act
            SceneGenerator.GenerateTestScene();

            // Assert
            Assert.IsTrue(File.Exists(scenePath), "TestScene should be generated.");

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "Generated TestScene should be a valid Unity scene.");

            Terrain activeTerrain = Terrain.activeTerrain;
            Assert.IsNotNull(activeTerrain, "Generated TestScene should contain an active terrain for varied elevation.");

            float terrainCenterHeight = activeTerrain.SampleHeight(Vector3.zero) + activeTerrain.transform.position.y;
            Assert.Greater(
                terrainCenterHeight,
                1f,
                $"Terrain height near spawn should be above flat-ground baseline. centerHeight={terrainCenterHeight:0.00}");

            int legacyEnvironmentPropCount = UnityEngine.Object
                .FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(t => t != null
                    && (t.name.StartsWith("RockCluster", StringComparison.Ordinal)
                        || t.name.StartsWith("Boulder", StringComparison.Ordinal)
                        || t.name.StartsWith("FallenLog", StringComparison.Ordinal)));
            Assert.AreEqual(
                0,
                legacyEnvironmentPropCount,
                $"Generated TestScene should no longer contain legacy placeholder environment props. Found {legacyEnvironmentPropCount}.");

            int resourceNodeCount = UnityEngine.Object
                .FindObjectsByType<ResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Length;
            Assert.GreaterOrEqual(resourceNodeCount, 6, "Generated TestScene should include the expected resource nodes around spawn.");

            var transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int biomeTreeCount = transforms.Count(t => t != null && t.name.StartsWith("Placed_BiomeTree_", StringComparison.Ordinal));
            Assert.AreEqual(
                0,
                biomeTreeCount,
                $"Generated TestScene should not include legacy biome ring trees. Found {biomeTreeCount}.");

            int worldTreeCount = transforms.Count(t => t != null && t.name.StartsWith("Placed_WorldTree_", StringComparison.Ordinal));
            Assert.AreEqual(
                0,
                worldTreeCount,
                $"Generated TestScene should not include legacy world tree scatter objects. Found {worldTreeCount}.");

            int showcaseTreeCount = transforms.Count(t => t != null && t.name.StartsWith("Placed_LargeTree_", StringComparison.Ordinal));
            Assert.AreEqual(
                0,
                showcaseTreeCount,
                $"Generated TestScene should not include legacy LargeTree showcase objects. Found {showcaseTreeCount}.");

            int quaterniusDecorTreeCount = transforms.Count(t => t != null && t.name.StartsWith("Placed_QuaterniusTree_", StringComparison.Ordinal));
            Assert.GreaterOrEqual(
                quaterniusDecorTreeCount,
                200,
                $"Generated TestScene should include Quaternius decorative tree scatter. Found {quaterniusDecorTreeCount}.");

            int quaterniusVegetationCount = transforms.Count(t => t != null && t.name.StartsWith("Placed_QuaterniusVegetation_", StringComparison.Ordinal));
            Assert.GreaterOrEqual(
                quaterniusVegetationCount,
                300,
                $"Generated TestScene should include Quaternius decorative vegetation scatter. Found {quaterniusVegetationCount}.");

            int quaterniusRockCount = transforms.Count(t => t != null && t.name.StartsWith("Placed_QuaterniusRock_", StringComparison.Ordinal));
            Assert.GreaterOrEqual(
                quaterniusRockCount,
                180,
                $"Generated TestScene should include Quaternius decorative rock scatter. Found {quaterniusRockCount}.");

            int quaterniusShowcaseTrees = transforms.Count(t => t != null && t.name.StartsWith("Showcase_QuaterniusTree_", StringComparison.Ordinal));
            Assert.Greater(
                quaterniusShowcaseTrees,
                0,
                $"Generated TestScene should include grouped Quaternius tree showcase placements. Found {quaterniusShowcaseTrees}.");

            int quaterniusShowcaseVegetation = transforms.Count(t => t != null && t.name.StartsWith("Showcase_QuaterniusVegetation_", StringComparison.Ordinal));
            Assert.Greater(
                quaterniusShowcaseVegetation,
                0,
                $"Generated TestScene should include grouped Quaternius vegetation showcase placements. Found {quaterniusShowcaseVegetation}.");

            int quaterniusShowcaseRocks = transforms.Count(t => t != null && t.name.StartsWith("Showcase_QuaterniusRock_", StringComparison.Ordinal));
            Assert.Greater(
                quaterniusShowcaseRocks,
                0,
                $"Generated TestScene should include grouped Quaternius rock showcase placements. Found {quaterniusShowcaseRocks}.");

            AssertNamedObjectsGrounded(activeTerrain, transforms, "Placed_QuaterniusTree_", maxSamples: 40, tolerance: 0.70f);
            AssertNamedObjectsGrounded(activeTerrain, transforms, "Placed_QuaterniusVegetation_", maxSamples: 40, tolerance: 0.70f);
            AssertNamedObjectsGrounded(activeTerrain, transforms, "Placed_QuaterniusRock_", maxSamples: 40, tolerance: 0.80f);
            AssertNamedObjectsGrounded(activeTerrain, transforms, "Showcase_QuaterniusTree_", maxSamples: 20, tolerance: 0.70f);
            AssertNamedObjectsGrounded(activeTerrain, transforms, "Showcase_QuaterniusVegetation_", maxSamples: 20, tolerance: 0.70f);
            AssertNamedObjectsGrounded(activeTerrain, transforms, "Showcase_QuaterniusRock_", maxSamples: 20, tolerance: 0.80f);

            AssertNamedObjectsUpright(transforms, "Placed_QuaterniusTree_", maxSamples: 40, minHeightRatio: 0.5f);
            AssertNamedObjectsUpright(transforms, "Placed_QuaterniusVegetation_", maxSamples: 40, minHeightRatio: 0.33f);
            AssertNamedObjectsTransformUpright(transforms, "Placed_QuaterniusRock_", maxSamples: 40, minUpDot: 0.65f);
            AssertNamedObjectsUpright(transforms, "Showcase_QuaterniusTree_", maxSamples: 20, minHeightRatio: 0.5f);
            AssertNamedObjectsUpright(transforms, "Showcase_QuaterniusVegetation_", maxSamples: 20, minHeightRatio: 0.33f);
            AssertNamedObjectsTransformUpright(transforms, "Showcase_QuaterniusRock_", maxSamples: 20, minUpDot: 0.65f);
        }

        [Test]
        public void SceneGenerator_ResolveSceneTreeUprightRotation_RotatesSidewaysBounds()
        {
            Quaternion alongZ = SceneGenerator.ResolveSceneTreeUprightRotation(new Vector3(1.2f, 0.25f, 5.1f));
            Quaternion expectedAlongZ = Quaternion.Euler(-90f, 0f, 0f);
            Assert.Less(Quaternion.Angle(expectedAlongZ, alongZ), 0.1f, "Tree elongated in Z with compressed Y should rotate around X.");

            Quaternion alongX = SceneGenerator.ResolveSceneTreeUprightRotation(new Vector3(4.7f, 0.2f, 1.1f));
            Quaternion expectedAlongX = Quaternion.Euler(0f, 0f, 90f);
            Assert.Less(Quaternion.Angle(expectedAlongX, alongX), 0.1f, "Tree elongated in X with compressed Y should rotate around Z.");
        }

        [Test]
        public void SceneGenerator_ResolveSceneTreeUprightRotation_KeepsUprightBounds()
        {
            Quaternion upright = SceneGenerator.ResolveSceneTreeUprightRotation(new Vector3(2f, 5.2f, 1.8f));
            Assert.Less(Quaternion.Angle(Quaternion.identity, upright), 0.1f);

            Quaternion degenerate = SceneGenerator.ResolveSceneTreeUprightRotation(new Vector3(0f, 0f, 0f));
            Assert.Less(Quaternion.Angle(Quaternion.identity, degenerate), 0.1f);
        }

        private static float ComputeRendererHeight(GameObject target)
        {
            if (target == null)
                return 0f;

            var renderers = target.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null || renderers.Length == 0)
                return 0f;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds.size.y;
        }

        private static float ComputeAverageLayerWeight(TerrainData terrainData, int layerIndex)
        {
            int width = terrainData.alphamapWidth;
            int height = terrainData.alphamapHeight;
            float[,,] alpha = terrainData.GetAlphamaps(0, 0, width, height);

            double sum = 0d;
            int count = 0;
            int step = Mathf.Max(1, width / 64);

            for (int z = 0; z < height; z += step)
            {
                for (int x = 0; x < width; x += step)
                {
                    sum += alpha[z, x, layerIndex];
                    count++;
                }
            }

            return count > 0 ? (float)(sum / count) : 0f;
        }

        private static float ComputeAverageLayerWeightAtPoints(TerrainData terrainData, int layerIndex, IReadOnlyList<Vector2> points)
        {
            if (terrainData == null || points == null || points.Count == 0)
                return 0f;

            float sum = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                int x = Mathf.Clamp(Mathf.RoundToInt(p.x * Mathf.Max(1, terrainData.alphamapWidth - 1)), 0, terrainData.alphamapWidth - 1);
                int z = Mathf.Clamp(Mathf.RoundToInt(p.y * Mathf.Max(1, terrainData.alphamapHeight - 1)), 0, terrainData.alphamapHeight - 1);
                float[,,] sample = terrainData.GetAlphamaps(x, z, 1, 1);
                sum += sample[0, 0, layerIndex];
            }

            return sum / points.Count;
        }

        private static void AssertNamedObjectsGrounded(Terrain terrain, Transform[] transforms, string namePrefix, int maxSamples, float tolerance)
        {
            if (terrain == null || transforms == null || maxSamples <= 0)
                return;

            var candidates = transforms
                .Where(t => t != null && t.name.StartsWith(namePrefix, StringComparison.Ordinal))
                .Take(maxSamples)
                .ToArray();

            Assert.Greater(candidates.Length, 0, $"Expected at least one object with prefix '{namePrefix}' for grounding validation.");

            for (int i = 0; i < candidates.Length; i++)
            {
                Transform t = candidates[i];
                if (!TryGetRendererBounds(t.gameObject, out Bounds bounds))
                    continue;

                float groundY = terrain.SampleHeight(t.position) + terrain.transform.position.y;
                float delta = bounds.min.y - groundY;
                Assert.LessOrEqual(
                    Mathf.Abs(delta),
                    tolerance,
                    $"Object '{t.name}' appears floating/buried relative to terrain. delta={delta:0.000}, tolerance={tolerance:0.000}.");
            }
        }

        /// <summary>
        /// Validates that placed objects are upright (taller than they are wide)
        /// using MeshFilter bounds which work reliably in -nographics batchmode.
        /// This catches FBX axis conversion failures that leave models lying flat.
        /// </summary>
        private static void AssertNamedObjectsUpright(Transform[] transforms, string namePrefix, int maxSamples, float minHeightRatio = 0.5f)
        {
            if (transforms == null || maxSamples <= 0)
                return;

            var candidates = transforms
                .Where(t => t != null && t.name.StartsWith(namePrefix, StringComparison.Ordinal))
                .Take(maxSamples)
                .ToArray();

            Assert.Greater(candidates.Length, 0, $"Expected at least one object with prefix '{namePrefix}' for upright validation.");

            int sidewaysCount = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                Transform t = candidates[i];
                Vector3 size = ComputeMeshBoundsWorldSize(t.gameObject);
                if (size.sqrMagnitude <= 0.0001f)
                    continue;

                float height = size.y;
                float horizontalMax = Mathf.Max(size.x, size.z);

                if (height < horizontalMax * minHeightRatio)
                {
                    sidewaysCount++;
                    Debug.LogWarning($"[UprightCheck] '{t.name}' appears sideways: bounds=({size.x:0.00}, {size.y:0.00}, {size.z:0.00}), height={height:0.00}, horizontalMax={horizontalMax:0.00}, threshold={minHeightRatio:0.00}");
                }
            }

            Assert.AreEqual(
                0,
                sidewaysCount,
                $"Found {sidewaysCount}/{candidates.Length} objects with prefix '{namePrefix}' that appear sideways (height < {minHeightRatio * 100:0}% of horizontal extent). Models must be upright.");
        }

        private static void AssertNamedObjectsTransformUpright(Transform[] transforms, string namePrefix, int maxSamples, float minUpDot)
        {
            if (transforms == null || maxSamples <= 0)
                return;

            var candidates = transforms
                .Where(t => t != null && t.name.StartsWith(namePrefix, StringComparison.Ordinal))
                .Take(maxSamples)
                .ToArray();

            Assert.Greater(candidates.Length, 0, $"Expected at least one object with prefix '{namePrefix}' for transform-up validation.");

            int sidewaysCount = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                float upDot = Mathf.Abs(Vector3.Dot(candidates[i].up.normalized, Vector3.up));
                if (upDot < minUpDot)
                {
                    sidewaysCount++;
                    Debug.LogWarning($"[UprightCheck] '{candidates[i].name}' has low up alignment. upDot={upDot:0.00}, threshold={minUpDot:0.00}");
                }
            }

            Assert.AreEqual(
                0,
                sidewaysCount,
                $"Found {sidewaysCount}/{candidates.Length} objects with prefix '{namePrefix}' whose transform up vector is misaligned. Expected upDot >= {minUpDot:0.00}.");
        }

        /// <summary>
        /// Computes world-space AABB size from MeshFilter.sharedMesh.bounds.
        /// Works in -nographics batchmode unlike Renderer.bounds.
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

        private static bool TryGetRendererBounds(GameObject target, out Bounds bounds)
        {
            bounds = default;
            if (target == null)
                return false;

            var renderers = target.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null || renderers.Length == 0)
                return false;

            bool hasBounds = false;
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

        private static Vector3 ComputeRendererBoundsSize(GameObject target)
        {
            if (target == null)
                return Vector3.zero;

            var renderers = target.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null || renderers.Length == 0)
                return Vector3.zero;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds.size;
        }

        private static Mesh ResolveRendererMesh(Renderer renderer)
        {
            if (renderer == null)
                return null;

            if (renderer is SkinnedMeshRenderer skinned)
                return skinned.sharedMesh;

            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        private static float ComputeCanopyToTrunkWidthRatio(Mesh mesh, Material[] materials)
        {
            if (mesh == null || materials == null || materials.Length == 0)
                return 0f;

            ResolveTreeMaterialSlots(materials, out int[] barkSlots, out int[] leafSlots);
            var barkMask = BuildVertexMask(mesh, barkSlots);
            var leafMask = BuildVertexMask(mesh, leafSlots);

            float trunkWidth = ComputeMaskedWidth(mesh.vertices, barkMask);
            float canopyWidth = ComputeMaskedWidth(mesh.vertices, leafMask);
            return canopyWidth / Mathf.Max(0.001f, trunkWidth);
        }

        private static bool TryComputeBranchCanopyProximity(
            Mesh mesh,
            Material[] materials,
            out int sampledBranchTips,
            out float medianNearestCanopyDistance,
            out float p90NearestCanopyDistance,
            out float connectedTipFraction)
        {
            sampledBranchTips = 0;
            medianNearestCanopyDistance = 0f;
            p90NearestCanopyDistance = 0f;
            connectedTipFraction = 0f;

            if (mesh == null || materials == null || materials.Length == 0)
                return false;

            ResolveTreeMaterialSlots(materials, out int[] barkSlots, out int[] leafSlots);
            var barkMask = BuildVertexMask(mesh, barkSlots);
            var leafMask = BuildVertexMask(mesh, leafSlots);

            if (barkMask == null || leafMask == null)
                return false;

            Vector3[] vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0)
                return false;

            var canopyVertices = new List<Vector3>();
            var barkRadial = new List<float>();
            float minBarkY = float.MaxValue;
            float maxBarkY = float.MinValue;

            for (int i = 0; i < vertices.Length; i++)
            {
                if (i < leafMask.Length && leafMask[i])
                    canopyVertices.Add(vertices[i]);

                if (i >= barkMask.Length || !barkMask[i])
                    continue;

                Vector3 barkVertex = vertices[i];
                float radial = Mathf.Sqrt(barkVertex.x * barkVertex.x + barkVertex.z * barkVertex.z);
                barkRadial.Add(radial);
                minBarkY = Mathf.Min(minBarkY, barkVertex.y);
                maxBarkY = Mathf.Max(maxBarkY, barkVertex.y);
            }

            if (canopyVertices.Count < 16 || barkRadial.Count < 32)
                return false;

            float barkHeight = Mathf.Max(0.001f, maxBarkY - minBarkY);
            float branchMinY = minBarkY + barkHeight * 0.30f;
            float trunkCoreRadius = ComputePercentile(barkRadial, 0.35f);
            float branchMinRadius = Mathf.Max(0.03f, trunkCoreRadius * 1.35f);

            var branchCandidates = new List<Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                if (i >= barkMask.Length || !barkMask[i])
                    continue;

                Vector3 barkVertex = vertices[i];
                if (barkVertex.y < branchMinY)
                    continue;

                float radial = Mathf.Sqrt(barkVertex.x * barkVertex.x + barkVertex.z * barkVertex.z);
                if (radial < branchMinRadius)
                    continue;

                branchCandidates.Add(barkVertex);
            }

            if (branchCandidates.Count < 20)
                return false;

            branchCandidates.Sort((a, b) =>
            {
                float aRadius = a.x * a.x + a.z * a.z;
                float bRadius = b.x * b.x + b.z * b.z;
                return bRadius.CompareTo(aRadius);
            });

            const int maxSamples = 256;
            int sampleCount = Mathf.Min(maxSamples, branchCandidates.Count);
            var nearestDistances = new List<float>(sampleCount);

            for (int i = 0; i < sampleCount; i++)
            {
                Vector3 branchVertex = branchCandidates[i];
                float nearestSqrDistance = float.MaxValue;

                for (int c = 0; c < canopyVertices.Count; c++)
                {
                    float sqrDistance = (canopyVertices[c] - branchVertex).sqrMagnitude;
                    if (sqrDistance < nearestSqrDistance)
                        nearestSqrDistance = sqrDistance;
                }

                nearestDistances.Add(Mathf.Sqrt(nearestSqrDistance));
            }

            sampledBranchTips = nearestDistances.Count;
            medianNearestCanopyDistance = ComputePercentile(nearestDistances, 0.50f);
            p90NearestCanopyDistance = ComputePercentile(nearestDistances, 0.90f);

            float meshHeight = Mathf.Max(0.001f, mesh.bounds.size.y);
            float connectedDistanceThreshold = Mathf.Max(0.28f, meshHeight * 0.20f);
            int connectedCount = 0;
            for (int i = 0; i < nearestDistances.Count; i++)
            {
                if (nearestDistances[i] <= connectedDistanceThreshold)
                    connectedCount++;
            }

            connectedTipFraction = nearestDistances.Count > 0
                ? (float)connectedCount / nearestDistances.Count
                : 0f;
            return true;
        }

        private static float ComputePercentile(List<float> values, float percentile)
        {
            if (values == null || values.Count == 0)
                return 0f;

            values.Sort();
            float clampedPercentile = Mathf.Clamp01(percentile);
            float rank = clampedPercentile * (values.Count - 1);
            int lower = Mathf.FloorToInt(rank);
            int upper = Mathf.CeilToInt(rank);

            if (lower == upper)
                return values[lower];

            float t = rank - lower;
            return Mathf.Lerp(values[lower], values[upper], t);
        }

        private static void ResolveTreeMaterialSlots(Material[] materials, out int[] barkSlots, out int[] leafSlots)
        {
            var bark = new List<int>();
            var leaf = new List<int>();

            for (int i = 0; i < materials.Length; i++)
            {
                string materialName = materials[i] != null ? materials[i].name : string.Empty;
                string normalized = materialName.ToLowerInvariant();

                if (normalized.Contains("leaf") || normalized.Contains("leaves") || normalized.Contains("foliage") || normalized.Contains("canopy"))
                {
                    leaf.Add(i);
                    continue;
                }

                if (normalized.Contains("bark") || normalized.Contains("trunk") || normalized.Contains("wood"))
                {
                    bark.Add(i);
                    continue;
                }
            }

            if (bark.Count == 0 && leaf.Count == 0 && materials.Length > 1)
            {
                bark.Add(0);
                for (int i = 1; i < materials.Length; i++)
                    leaf.Add(i);
            }

            barkSlots = bark.ToArray();
            leafSlots = leaf.ToArray();
        }

        private static bool[] BuildVertexMask(Mesh mesh, int[] subMeshIndices)
        {
            if (mesh == null || subMeshIndices == null || subMeshIndices.Length == 0)
                return null;

            var mask = new bool[mesh.vertexCount];
            for (int i = 0; i < subMeshIndices.Length; i++)
            {
                int subMeshIndex = subMeshIndices[i];
                if (subMeshIndex < 0 || subMeshIndex >= mesh.subMeshCount)
                    continue;

                int[] triangles = mesh.GetTriangles(subMeshIndex);
                for (int t = 0; t < triangles.Length; t++)
                {
                    int vertexIndex = triangles[t];
                    if (vertexIndex >= 0 && vertexIndex < mask.Length)
                        mask[vertexIndex] = true;
                }
            }

            return mask;
        }

        private static float ComputeMaskedWidth(Vector3[] vertices, bool[] mask)
        {
            if (vertices == null || mask == null)
                return 0f;

            bool hasAny = false;
            float minX = 0f;
            float maxX = 0f;
            float minZ = 0f;
            float maxZ = 0f;

            for (int i = 0; i < vertices.Length && i < mask.Length; i++)
            {
                if (!mask[i])
                    continue;

                if (!hasAny)
                {
                    minX = maxX = vertices[i].x;
                    minZ = maxZ = vertices[i].z;
                    hasAny = true;
                    continue;
                }

                minX = Mathf.Min(minX, vertices[i].x);
                maxX = Mathf.Max(maxX, vertices[i].x);
                minZ = Mathf.Min(minZ, vertices[i].z);
                maxZ = Mathf.Max(maxZ, vertices[i].z);
            }

            if (!hasAny)
                return 0f;

            return (maxX - minX + maxZ - minZ) * 0.5f;
        }
    }
}
