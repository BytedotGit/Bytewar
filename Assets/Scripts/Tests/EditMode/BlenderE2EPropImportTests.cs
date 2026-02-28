using System.IO;
using System.Collections.Generic;
using System;
using ByteWar.Editor;
using ByteWar.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using ByteWar.Survival;
using Object = UnityEngine.Object;

namespace ByteWar.Tests.EditMode
{
    public class BlenderE2EPropImportTests
    {
        private const string FbxPath = "Assets/Art/Environment/Props/BlenderE2EProp/BlenderE2EProp.fbx";
        private const string PrefabPath = "Assets/Resources/Generated/BlenderE2EProp/BlenderE2EProp.prefab";
        private const string PreviewPngPath = "Assets/Art/Environment/Props/BlenderE2EProp/Preview.png";
        private const string LargeTreeFbxPath = "Assets/Art/Environment/Vegetation/LargeTree/LargeTree.fbx";
        private const string LargeTreePrefabPath = "Assets/Resources/Generated/LargeTree/LargeTree.prefab";
        private const string LargeTreePreviewPngPath = "Assets/Art/Environment/Vegetation/LargeTree/Preview.png";
        private const string LargeTreeBarkTexturePath = "Assets/Art/Environment/Vegetation/LargeTree/Textures/LargeTree_Bark_Albedo.png";
        private const string LargeTreeLeavesTexturePath = "Assets/Art/Environment/Vegetation/LargeTree/Textures/LargeTree_Leaves_Albedo.png";
        private const string LargeTreeBarkMaterialPath = "Assets/Art/Environment/Vegetation/LargeTree/Materials/LargeTree_Bark.mat";
        private const string LargeTreeLeavesMaterialPath = "Assets/Art/Environment/Vegetation/LargeTree/Materials/LargeTree_Leaves.mat";
        private const string LargeTreeVariationArtRoot = "Assets/Art/Environment/Vegetation/LargeTree/Variants";
        private const string LargeTreeVariationGeneratedRoot = "Assets/Resources/Generated/LargeTree/Variants";
        private const string DeployableCatalogAssetPath = "Assets/Resources/Generated/DeployableAssetCatalog.asset";
        private const string QuaterniusNatureSourceRoot = "Assets/Art/Premade/Quaternius/UltimateNature/FBX";
        private const string QuaterniusCropsSourceRoot = "Assets/Art/Premade/Quaternius/UltimateCrops/FBX";

        [Test]
        public void BlenderE2EPropPrefab_HasLodsUvsAndSingleRootCollider_WhenAssetPresent()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (model == null)
                Assert.Ignore($"Blender E2E FBX not found at '{FbxPath}'. Run Blender MCP tool 'blender_generate_e2e_prop' to generate it.");

            Assert.IsTrue(BlenderE2EPropPrefabGenerator.Generate(), "Prefab generator did not generate the prefab.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"Expected prefab at '{PrefabPath}' after generation.");

            var instance = Object.Instantiate(prefab);
            try
            {
                var lodGroup = instance.GetComponent<LODGroup>();
                Assert.IsNotNull(lodGroup, "LODGroup missing on prefab root.");

                var lods = lodGroup.GetLODs();
                Assert.AreEqual(3, lods.Length, "Expected exactly 3 LODs.");

                int lod0Tris = SumTrianglesAndValidateUvs(lods[0], "LOD0");
                int lod1Tris = SumTrianglesAndValidateUvs(lods[1], "LOD1");
                int lod2Tris = SumTrianglesAndValidateUvs(lods[2], "LOD2");

                if (lod0Tris == lod1Tris && lod1Tris == lod2Tris)
                {
                    Assert.Greater(lod0Tris, 0, "Fallback LODs should still contain renderable geometry.");
                }
                else
                {
                    Assert.Greater(lod0Tris, lod1Tris, "Expected LOD0 triangles > LOD1 triangles.");
                    Assert.Greater(lod1Tris, lod2Tris, "Expected LOD1 triangles > LOD2 triangles.");
                }

                var colliders = instance.GetComponentsInChildren<Collider>(includeInactive: true);
                Assert.AreEqual(1, colliders.Length, "Expected exactly 1 collider in the prefab hierarchy.");
                Assert.AreSame(instance, colliders[0].gameObject, "Expected the single collider to be on the prefab root.");

                Assert.IsFalse(colliders[0].isTrigger, "Expected BlenderE2EProp collider to be solid (isTrigger=false).");

                Assert.IsNotNull(instance.GetComponent<NetworkObject>(), "BlenderE2EProp root should include NetworkObject for server-authoritative deploy spawning.");
                Assert.IsNotNull(instance.GetComponent<NetworkTransform>(), "BlenderE2EProp root should include NetworkTransform for NGO registration compatibility.");

                if (colliders[0] is MeshCollider mc && mc.sharedMesh != null)
                {
                    // Bounds are in mesh local space; for our root collider this should closely match the visible prop.
                    Bounds b = mc.sharedMesh.bounds;
                    Assert.GreaterOrEqual(b.min.y, -0.10f, $"Collider bounds min.y too low (min.y={b.min.y:0.000}). Likely unbaked offset.");
                    Assert.Greater(b.max.y, 0.50f, $"Collider bounds max.y too low (max.y={b.max.y:0.000}). Collider likely not covering prop top.");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            // Visual preview is produced via Blender headless render. Keep this non-fatal in CI.
            if (!File.Exists(PreviewPngPath))
            {
                Debug.LogWarning($"[BlenderE2EPropImportTests] Preview image not found at '{PreviewPngPath}'. Run Blender MCP tool 'blender_render_preview' to generate it.");
            }
            else
            {
                var info = new FileInfo(PreviewPngPath);
                Assert.Greater(info.Length, 0, "Preview.png exists but is empty.");
            }
        }

        [Test]
        public void LargeTreePrefab_HasNetworkRootLodsUvsAndSingleRootCollider_WhenAssetPresent()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(LargeTreeFbxPath);
            if (model == null)
                Assert.Ignore($"LargeTree FBX not found at '{LargeTreeFbxPath}'. Run Blender MCP tool 'blender_generate_large_tree' to generate it.");

            Assert.IsTrue(LargeTreePrefabGenerator.Generate(), "LargeTree prefab generator did not generate the prefab.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LargeTreePrefabPath);
            Assert.IsNotNull(prefab, $"Expected prefab at '{LargeTreePrefabPath}' after generation.");

            var instance = Object.Instantiate(prefab);
            try
            {
                Assert.IsNotNull(instance.GetComponent<NetworkObject>(), "NetworkObject missing on LargeTree prefab root.");
                Assert.IsNotNull(instance.GetComponent<NetworkTransform>(), "NetworkTransform missing on LargeTree prefab root.");

                var lodGroup = instance.GetComponent<LODGroup>();
                Assert.IsNotNull(lodGroup, "LODGroup missing on LargeTree prefab root.");

                var lods = lodGroup.GetLODs();
                Assert.AreEqual(3, lods.Length, "Expected exactly 3 LODs for LargeTree.");

                int lod0Tris = SumTrianglesAndValidateUvs(lods[0], "LOD0");
                int lod1Tris = SumTrianglesAndValidateUvs(lods[1], "LOD1");
                int lod2Tris = SumTrianglesAndValidateUvs(lods[2], "LOD2");

                if (lod0Tris == lod1Tris && lod1Tris == lod2Tris)
                {
                    Assert.Greater(lod0Tris, 0, "Fallback LODs should still contain renderable geometry.");
                }
                else
                {
                    Assert.Greater(lod0Tris, lod1Tris, "Expected LargeTree LOD0 triangles > LOD1 triangles.");
                    Assert.Greater(lod1Tris, lod2Tris, "Expected LargeTree LOD1 triangles > LOD2 triangles.");
                }

                var colliders = instance.GetComponentsInChildren<Collider>(includeInactive: true);
                Assert.AreEqual(1, colliders.Length, "Expected exactly 1 collider in LargeTree prefab hierarchy.");
                Assert.AreSame(instance, colliders[0].gameObject, "Expected LargeTree collider to be on prefab root.");
                Assert.IsFalse(colliders[0].isTrigger, "Expected LargeTree collider to be solid (isTrigger=false).");

                bool sawBark = false;
                bool sawLeaves = false;
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
                {
                    if (renderer == null)
                        continue;

                    if (renderer.gameObject.name.EndsWith("_COL", System.StringComparison.OrdinalIgnoreCase) ||
                        renderer.gameObject.name.EndsWith("_TRIG", System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.IsNotNull(material, $"Renderer '{renderer.name}' has a null material slot.");
                        Assert.IsNotNull(material.mainTexture, $"Material '{material.name}' on renderer '{renderer.name}' is missing a texture.");

                        string n = material.name.ToLowerInvariant();
                        if (n.Contains("bark") || n.Contains("trunk"))
                            sawBark = true;
                        if (n.Contains("leaf") || n.Contains("leaves") || n.Contains("canopy"))
                            sawLeaves = true;
                    }
                }

                Assert.IsTrue(sawBark, "Expected at least one bark material assignment on LargeTree renderers.");
                Assert.IsTrue(sawLeaves, "Expected at least one leaves material assignment on LargeTree renderers.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            var barkTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(LargeTreeBarkTexturePath);
            var leavesTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(LargeTreeLeavesTexturePath);
            var barkMaterial = AssetDatabase.LoadAssetAtPath<Material>(LargeTreeBarkMaterialPath);
            var leavesMaterial = AssetDatabase.LoadAssetAtPath<Material>(LargeTreeLeavesMaterialPath);

            Assert.IsNotNull(barkTexture, $"Expected bark texture asset at '{LargeTreeBarkTexturePath}'.");
            Assert.IsNotNull(leavesTexture, $"Expected leaves texture asset at '{LargeTreeLeavesTexturePath}'.");
            Assert.IsNotNull(barkMaterial, $"Expected bark material asset at '{LargeTreeBarkMaterialPath}'.");
            Assert.IsNotNull(leavesMaterial, $"Expected leaves material asset at '{LargeTreeLeavesMaterialPath}'.");
            Assert.AreSame(barkTexture, barkMaterial.mainTexture, "Bark material should reference the generated bark texture.");
            Assert.AreSame(leavesTexture, leavesMaterial.mainTexture, "Leaves material should reference the generated leaves texture.");

            if (!File.Exists(LargeTreePreviewPngPath))
            {
                Debug.LogWarning($"[BlenderE2EPropImportTests] LargeTree preview image not found at '{LargeTreePreviewPngPath}'. Run Blender MCP tool 'blender_render_large_tree_preview' to generate it.");
            }
            else
            {
                var info = new FileInfo(LargeTreePreviewPngPath);
                Assert.Greater(info.Length, 0, "LargeTree Preview.png exists but is empty.");
            }
        }

        [Test]
        public void LargeTreeVariationPrefabs_GenerateWithLodsAndCollider_WhenVariationAssetsPresent()
        {
            int sourceCount = 0;
            foreach (var variation in LargeTreeVariationPrefabGenerator.KnownVariations)
            {
                string sourceFbxPath = $"{LargeTreeVariationArtRoot}/{variation}/LargeTree_{variation}.fbx";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(sourceFbxPath) != null)
                    sourceCount++;
            }

            if (sourceCount == 0)
            {
                Assert.Ignore(
                    "No LargeTree variation FBX files found. Run Blender MCP variation generation first.");
            }

            Assert.IsTrue(
                LargeTreeVariationPrefabGenerator.GenerateAll(),
                "Variation prefab generation should succeed when source FBXs are present.");

            foreach (var variation in LargeTreeVariationPrefabGenerator.KnownVariations)
            {
                string sourceFbxPath = $"{LargeTreeVariationArtRoot}/{variation}/LargeTree_{variation}.fbx";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(sourceFbxPath) == null)
                    continue;

                string prefabPath = $"{LargeTreeVariationGeneratedRoot}/{variation}/LargeTree_{variation}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.IsNotNull(prefab, $"Expected generated variation prefab at '{prefabPath}'.");

                var instance = Object.Instantiate(prefab);
                try
                {
                    var lodGroup = instance.GetComponent<LODGroup>();
                    Assert.IsNotNull(lodGroup, $"LODGroup missing on variation prefab '{variation}'.");
                    Assert.AreEqual(3, lodGroup.GetLODs().Length, $"Variation '{variation}' should have 3 LOD levels.");

                    var collider = instance.GetComponent<Collider>();
                    Assert.IsNotNull(collider, $"Collider missing on variation prefab '{variation}'.");
                    Assert.IsFalse(collider.isTrigger, $"Variation prefab '{variation}' collider should be solid.");

                    Assert.IsNotNull(instance.GetComponent<NetworkObject>(), $"Variation prefab '{variation}' should include NetworkObject on root for server-authoritative deploy spawning.");
                    Assert.IsNotNull(instance.GetComponent<NetworkTransform>(), $"Variation prefab '{variation}' should include NetworkTransform on root for NGO registration compatibility.");
                    Assert.IsNotNull(instance.GetComponent<ResourceNode>(), $"Variation prefab '{variation}' should include ResourceNode for harvest gameplay.");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }

                string sourcePreviewPath = $"{LargeTreeVariationArtRoot}/{variation}/Preview.png";
                string generatedPreviewPath = $"{LargeTreeVariationGeneratedRoot}/{variation}/Preview.png";
                if (File.Exists(sourcePreviewPath))
                {
                    Assert.IsTrue(File.Exists(generatedPreviewPath), $"Expected generated preview at '{generatedPreviewPath}'.");
                    var info = new FileInfo(generatedPreviewPath);
                    Assert.Greater(info.Length, 0, $"Generated preview '{generatedPreviewPath}' exists but is empty.");
                }
            }

            Assert.IsTrue(
                DeployableAssetCatalogBuilder.TryRegenerate(out _),
                "DeployableAssetCatalog regeneration should succeed after variation prefab generation.");

            var catalog = AssetDatabase.LoadAssetAtPath<DeployableAssetCatalog>(DeployableCatalogAssetPath);
            Assert.IsNotNull(catalog, "DeployableAssetCatalog asset should exist after regeneration.");

            foreach (var variation in LargeTreeVariationPrefabGenerator.KnownVariations)
            {
                string sourceFbxPath = $"{LargeTreeVariationArtRoot}/{variation}/LargeTree_{variation}.fbx";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(sourceFbxPath) == null)
                    continue;

                string expectedResourcePath = $"Generated/LargeTree/Variants/{variation}/LargeTree_{variation}";
                Assert.IsTrue(
                    CatalogContainsResourcePath(catalog.Entries, expectedResourcePath),
                    $"DeployableAssetCatalog should include variation resource path '{expectedResourcePath}'.");
            }
        }

        [Test]
        public void DeployableCatalog_IncludesQuaterniusPremadeModels_WhenSourcesPresent()
        {
            var expectedResourcePaths = GatherExpectedQuaterniusResourcePaths();
            if (expectedResourcePaths.Count == 0)
                Assert.Ignore("No renderable Quaternius source FBX assets found.");

            Assert.IsTrue(
                DeployableAssetCatalogBuilder.TryRegenerate(out _),
                "DeployableAssetCatalog regeneration should succeed for Quaternius deployables.");

            var catalog = AssetDatabase.LoadAssetAtPath<DeployableAssetCatalog>(DeployableCatalogAssetPath);
            Assert.IsNotNull(catalog, "DeployableAssetCatalog asset should exist after regeneration.");
            Assert.IsNotNull(catalog.Entries, "DeployableAssetCatalog entries should be initialized.");

            for (int i = 0; i < expectedResourcePaths.Count; i++)
            {
                string expectedPath = expectedResourcePaths[i];
                Assert.IsTrue(
                    CatalogContainsResourcePath(catalog.Entries, expectedPath),
                    $"DeployableAssetCatalog should include Quaternius resource path '{expectedPath}'.");

                var prefab = Resources.Load<GameObject>(expectedPath);
                Assert.IsNotNull(prefab, $"Resources should load Quaternius deployable '{expectedPath}'.");
                Assert.IsTrue(
                    AssetDeployUI.HasRequiredDeveloperSpawnComponents(prefab),
                    $"Quaternius deployable '{expectedPath}' should be server-placement-ready (NetworkObject/Collider/LODGroup).");
            }
        }

        private static bool CatalogContainsResourcePath(IReadOnlyList<DeployableAssetCatalog.Entry> entries, string expectedResourcePath)
        {
            if (entries == null || string.IsNullOrWhiteSpace(expectedResourcePath))
                return false;

            string expected = NormalizeResourcePath(expectedResourcePath);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;

                string actual = NormalizeResourcePath(entry.ResourcePath);
                if (string.Equals(actual, expected, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static List<string> GatherExpectedQuaterniusResourcePaths()
        {
            var results = new List<string>();
            AddExpectedQuaterniusResourcePathsForRoot(QuaterniusNatureSourceRoot, "UltimateNature", results);
            AddExpectedQuaterniusResourcePathsForRoot(QuaterniusCropsSourceRoot, "UltimateCrops", results);
            results.Sort(StringComparer.Ordinal);
            return results;
        }

        private static void AddExpectedQuaterniusResourcePathsForRoot(string sourceRoot, string family, List<string> output)
        {
            if (!AssetDatabase.IsValidFolder(sourceRoot) || output == null)
                return;

            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { sourceRoot });
            Array.Sort(guids, StringComparer.Ordinal);

            for (int i = 0; i < guids.Length; i++)
            {
                string sourcePath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(sourcePath) || !sourcePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    continue;

                var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (sourcePrefab == null)
                    continue;

                var renderers = sourcePrefab.GetComponentsInChildren<Renderer>(includeInactive: true);
                if (renderers == null || renderers.Length == 0)
                    continue;

                string relative = sourcePath.Substring(sourceRoot.Length).TrimStart('/', '\\');
                string withoutExt = Path.ChangeExtension(relative, null)?.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(withoutExt))
                    continue;

                output.Add($"Generated/Quaternius/{family}/{withoutExt}");
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

            return normalized;
        }

        private static int SumTrianglesAndValidateUvs(LOD lod, string label)
        {
            Assert.IsNotNull(lod.renderers, $"{label}: renderers array is null.");
            Assert.Greater(lod.renderers.Length, 0, $"{label}: expected at least one renderer.");

            int triCount = 0;
            foreach (var r in lod.renderers)
            {
                Assert.IsNotNull(r, $"{label}: renderer is null.");

                Mesh mesh = null;
                if (r is SkinnedMeshRenderer smr)
                    mesh = smr.sharedMesh;
                else
                    mesh = r.GetComponent<MeshFilter>() != null ? r.GetComponent<MeshFilter>().sharedMesh : null;

                Assert.IsNotNull(mesh, $"{label}: mesh missing on renderer '{r.name}'.");
                Assert.Greater(mesh.uv.Length, 0, $"{label}: mesh '{mesh.name}' has no UV0 (uv.Length == 0).");

                triCount += mesh.triangles.Length / 3;
            }

            Assert.Greater(triCount, 0, $"{label}: total triangles must be > 0.");
            return triCount;
        }
    }
}
