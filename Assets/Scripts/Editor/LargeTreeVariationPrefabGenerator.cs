using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using ByteWar.Survival;

namespace ByteWar.Editor
{
    /// <summary>
    /// Generates runtime-ready prefabs for LargeTree style variations exported by Blender MCP.
    /// Output path: Assets/Resources/Generated/LargeTree/Variants/{Variation}/LargeTree_{Variation}.prefab
    /// </summary>
    public static class LargeTreeVariationPrefabGenerator
    {
        internal const string VariationArtRoot = "Assets/Art/Environment/Vegetation/LargeTree/Variants";
        internal const string VariationGeneratedRoot = "Assets/Resources/Generated/LargeTree/Variants";

        private static readonly string[] VariationNames =
        {
            "Seedling",
            "Sapling",
            "Young",
            "Mature",
            "Adult",
        };

        private const string WoodItemAssetPath = "Assets/GeneratedAssets/Wood.asset";

        private readonly struct VariationHarvestConfig
        {
            public readonly float MaxHealth;
            public readonly int DropAmount;

            public VariationHarvestConfig(float maxHealth, int dropAmount)
            {
                MaxHealth = maxHealth;
                DropAmount = Mathf.Max(1, dropAmount);
            }
        }

        private readonly struct VariationVisualConfig
        {
            public readonly float UniformScale;
            public readonly float TrunkWidthScale;
            public readonly float TrunkTopScale;
            public readonly float TrunkHeightScale;
            public readonly float TrunkLean;
            public readonly float CanopyRadialScale;
            public readonly float CanopyVerticalScale;
            public readonly float CanopyTopPruneStrength;
            public readonly float CanopyYOffset;

            public VariationVisualConfig(
                float uniformScale,
                float trunkWidthScale,
                float trunkTopScale,
                float trunkHeightScale,
                float trunkLean,
                float canopyRadialScale,
                float canopyVerticalScale,
                float canopyTopPruneStrength,
                float canopyYOffset)
            {
                UniformScale = Mathf.Max(0.05f, uniformScale);
                TrunkWidthScale = Mathf.Clamp(trunkWidthScale, 0.20f, 2.00f);
                TrunkTopScale = Mathf.Clamp(trunkTopScale, 0.20f, 2.00f);
                TrunkHeightScale = Mathf.Clamp(trunkHeightScale, 0.50f, 2.00f);
                TrunkLean = Mathf.Clamp(trunkLean, -0.35f, 0.35f);
                CanopyRadialScale = Mathf.Clamp(canopyRadialScale, 0.20f, 2.00f);
                CanopyVerticalScale = Mathf.Clamp(canopyVerticalScale, 0.20f, 2.00f);
                CanopyTopPruneStrength = Mathf.Clamp01(canopyTopPruneStrength);
                CanopyYOffset = Mathf.Clamp(canopyYOffset, -1.0f, 1.0f);
            }
        }

        public static IReadOnlyList<string> KnownVariations => VariationNames;

        public static bool GenerateAll()
        {
            bool anySourceFound = false;
            bool allSucceeded = true;
            int generatedCount = 0;

            for (int i = 0; i < VariationNames.Length; i++)
            {
                string variation = VariationNames[i];
                if (!HasSourceFbx(variation))
                {
                    Debug.Log($"[LargeTreeVariationPrefabGenerator] Variation '{variation}' source FBX missing. Skipping.");
                    continue;
                }

                anySourceFound = true;
                bool ok = GenerateVariation(variation);
                allSucceeded &= ok;
                if (ok)
                    generatedCount++;
            }

            if (!anySourceFound)
            {
                Debug.LogWarning("[LargeTreeVariationPrefabGenerator] No LargeTree variation FBX sources found. Skipping generation.");
                return false;
            }

            bool catalogOk = DeployableAssetCatalogBuilder.TryRegenerate(out string catalogMsg);
            if (catalogOk)
                Debug.Log($"[LargeTreeVariationPrefabGenerator] {catalogMsg}");
            else
                Debug.LogWarning($"[LargeTreeVariationPrefabGenerator] {catalogMsg}");

            Debug.Log($"[LargeTreeVariationPrefabGenerator] Variation generation summary: generated={generatedCount}/{VariationNames.Length}, success={allSucceeded}.");
            return allSucceeded;
        }

        public static bool GenerateVariation(string variationName)
        {
            string safeVariation = NormalizeVariationName(variationName);
            if (string.IsNullOrEmpty(safeVariation))
            {
                Debug.LogWarning("[LargeTreeVariationPrefabGenerator] Variation name was empty after normalization.");
                return false;
            }

            string assetName = $"LargeTree_{safeVariation}";
            string fbxPath = GetVariationFbxPath(safeVariation);
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
            {
                Debug.LogWarning($"[LargeTreeVariationPrefabGenerator] FBX missing at '{fbxPath}'. Skipping variation '{safeVariation}'.");
                return false;
            }

            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "Generated");
            EnsureFolder("Assets/Resources/Generated", "LargeTree");
            EnsureFolder("Assets/Resources/Generated/LargeTree", "Variants");
            EnsureFolder("Assets/Resources/Generated/LargeTree/Variants", safeVariation);

            TryCopyPreviewIntoResources(safeVariation);

            var root = new GameObject(assetName);
            try
            {
                if (root.GetComponent<NetworkObject>() == null)
                    root.AddComponent<NetworkObject>();
                if (root.GetComponent<NetworkTransform>() == null)
                    root.AddComponent<NetworkTransform>();

                var visualRoot = new GameObject("VisualRoot");
                visualRoot.transform.SetParent(root.transform, worldPositionStays: false);
                var visualConfig = ResolveVisualConfig(safeVariation);
                visualRoot.transform.localScale = Vector3.one * visualConfig.UniformScale;

                var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                modelInstance.name = assetName;
                modelInstance.transform.SetParent(visualRoot.transform, worldPositionStays: false);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;

                ApplyUprightCorrectionIfNeeded(modelInstance, safeVariation);

                Debug.Log($"[LargeTreeVariationPrefabGenerator] Visual profile '{safeVariation}' -> scale={visualConfig.UniformScale:0.00}.");

                if (!LargeTreeProductionMaterialPipeline.TryEnsureAndAssign(modelInstance, out string materialMsg))
                {
                    Debug.LogWarning($"[LargeTreeVariationPrefabGenerator] {safeVariation}: {materialMsg}");
                    return false;
                }

                if (!TryApplyStageSilhouette(modelInstance, safeVariation, assetName, visualConfig, out string silhouetteMsg))
                {
                    Debug.LogWarning($"[LargeTreeVariationPrefabGenerator] {safeVariation}: {silhouetteMsg}");
                    return false;
                }

                NormalizeToTargetWorldHeight(visualRoot.transform, modelInstance, safeVariation);

                Debug.Log($"[LargeTreeVariationPrefabGenerator] {safeVariation}: {silhouetteMsg}");

                var modelGroups = modelInstance.GetComponentsInChildren<LODGroup>(includeInactive: true);
                if (modelGroups != null)
                {
                    for (int i = 0; i < modelGroups.Length; i++)
                    {
                        if (modelGroups[i] != null)
                            UnityEngine.Object.DestroyImmediate(modelGroups[i]);
                    }
                }

                if (!TrySetupLods(root, modelInstance, out string lodMsg))
                {
                    Debug.LogWarning($"[LargeTreeVariationPrefabGenerator] {safeVariation}: {lodMsg}");
                    return false;
                }

                SetupSingleRootCollider(root, modelInstance, assetName, safeVariation);
                StripChildColliders(root);
                DisableColliderMeshRendering(modelInstance, assetName);
                ConfigureResourceNode(root, safeVariation);

                string prefabPath = GetVariationPrefabPath(safeVariation);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);

                TryCopyPreviewIntoResources(safeVariation);

                Debug.Log($"[LargeTreeVariationPrefabGenerator] Generated variation prefab: {prefabPath}");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static string GetVariationFbxPath(string variationName)
            => $"{VariationArtRoot}/{variationName}/LargeTree_{variationName}.fbx";

        private static string GetVariationPrefabPath(string variationName)
            => $"{VariationGeneratedRoot}/{variationName}/LargeTree_{variationName}.prefab";

        private static bool HasSourceFbx(string variationName)
            => AssetDatabase.LoadAssetAtPath<GameObject>(GetVariationFbxPath(variationName)) != null;

        private static string NormalizeVariationName(string variationName)
        {
            if (string.IsNullOrWhiteSpace(variationName))
                return string.Empty;

            var chars = variationName.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                bool ok = char.IsLetterOrDigit(chars[i]) || chars[i] == '_' || chars[i] == '-';
                if (!ok)
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static void TryCopyPreviewIntoResources(string variationName)
        {
            string artPreviewPath = $"{VariationArtRoot}/{variationName}/Preview.png";
            string resourcesPreviewPath = $"{VariationGeneratedRoot}/{variationName}/Preview.png";

            try
            {
                if (!File.Exists(artPreviewPath))
                    return;

                File.Copy(artPreviewPath, resourcesPreviewPath, overwrite: true);
                AssetDatabase.ImportAsset(resourcesPreviewPath, ImportAssetOptions.ForceUpdate);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LargeTreeVariationPrefabGenerator] Failed to copy variation preview for '{variationName}': {ex.Message}");
            }
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = string.IsNullOrEmpty(parent) ? name : $"{parent}/{name}";
            if (AssetDatabase.IsValidFolder(path))
                return;

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                string[] parts = parent.Split('/');
                if (parts.Length >= 2)
                {
                    string current = parts[0];
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string next = $"{current}/{parts[i]}";
                        if (!AssetDatabase.IsValidFolder(next))
                            AssetDatabase.CreateFolder(current, parts[i]);
                        current = next;
                    }
                }
            }

            AssetDatabase.CreateFolder(parent, name);
        }

        private static void ApplyUprightCorrectionIfNeeded(GameObject modelRoot, string variationName)
        {
            if (modelRoot == null)
                return;

            Vector3 renderableSize = ComputeRenderableBoundsSize(modelRoot);
            Quaternion correction = ResolveTreeUprightRotation(renderableSize);
            if (correction == Quaternion.identity)
            {
                Debug.Log($"[LargeTreeVariationPrefabGenerator] {variationName}: orientation kept as-is (size={renderableSize}).");
                return;
            }

            modelRoot.transform.localRotation = correction * modelRoot.transform.localRotation;
            Debug.Log($"[LargeTreeVariationPrefabGenerator] {variationName}: applied upright correction {correction.eulerAngles} (pre-correction size={renderableSize}).");
        }

        public static Quaternion ResolveTreeUprightRotation(Vector3 renderableSize)
        {
            float sx = Mathf.Abs(renderableSize.x);
            float sy = Mathf.Abs(renderableSize.y);
            float sz = Mathf.Abs(renderableSize.z);

            float horizontalMax = Mathf.Max(sx, sz);
            if (horizontalMax <= 0.001f)
                return Quaternion.identity;

            // Trees can be wider than they are tall, but if Y is heavily compressed
            // relative to horizontal extents, the source mesh is likely lying flat.
            if (sy >= horizontalMax * 0.55f)
                return Quaternion.identity;

            if (sz >= sx)
                return Quaternion.Euler(-90f, 0f, 0f);

            return Quaternion.Euler(0f, 0f, 90f);
        }

        private static Vector3 ComputeRenderableBoundsSize(GameObject modelRoot)
        {
            if (modelRoot == null)
                return Vector3.zero;

            bool hasBounds = false;
            Bounds aggregate = default;

            var renderers = modelRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                string name = renderer.gameObject.name;
                if (name.EndsWith("_COL", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("_TRIG", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!hasBounds)
                {
                    aggregate = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                aggregate.Encapsulate(renderer.bounds);
            }

            return hasBounds ? aggregate.size : Vector3.zero;
        }

        private static List<Renderer> CollectRenderableVisuals(GameObject modelRoot)
        {
            var renderers = new List<Renderer>();
            foreach (var renderer in modelRoot.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                if (renderer == null)
                    continue;

                string name = renderer.gameObject.name;
                if (name.EndsWith("_COL", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("_TRIG", StringComparison.OrdinalIgnoreCase))
                    continue;

                renderers.Add(renderer);
            }

            return renderers;
        }

        private static bool TrySetupLods(GameObject prefabRoot, GameObject modelRoot, out string message)
        {
            var lod0 = new List<Renderer>();
            var lod1 = new List<Renderer>();
            var lod2 = new List<Renderer>();

            foreach (var tr in modelRoot.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (tr == null) continue;
                string n = tr.name;
                if (n.EndsWith("_LOD0", StringComparison.OrdinalIgnoreCase))
                    lod0.AddRange(tr.GetComponentsInChildren<Renderer>(includeInactive: true));
                else if (n.EndsWith("_LOD1", StringComparison.OrdinalIgnoreCase))
                    lod1.AddRange(tr.GetComponentsInChildren<Renderer>(includeInactive: true));
                else if (n.EndsWith("_LOD2", StringComparison.OrdinalIgnoreCase))
                    lod2.AddRange(tr.GetComponentsInChildren<Renderer>(includeInactive: true));
            }

            lod0.RemoveAll(r => r == null);
            lod1.RemoveAll(r => r == null);
            lod2.RemoveAll(r => r == null);

            if (lod0.Count == 0 || lod1.Count == 0 || lod2.Count == 0)
            {
                // Fallback: no LOD-suffixed children. Use all renderable visuals for flat LODs.
                var fallbackRenderers = CollectRenderableVisuals(modelRoot);
                if (fallbackRenderers.Count == 0)
                {
                    message = $"LODGroup not configured: missing renderers (LOD0={lod0.Count}, LOD1={lod1.Count}, LOD2={lod2.Count}) and no fallback renderers found.";
                    return false;
                }

                var fallbackGroup = prefabRoot.GetComponent<LODGroup>();
                if (fallbackGroup == null)
                    fallbackGroup = prefabRoot.AddComponent<LODGroup>();

                fallbackGroup.fadeMode = LODFadeMode.None;
                fallbackGroup.animateCrossFading = false;
                var fallbackArray = fallbackRenderers.ToArray();
                fallbackGroup.SetLODs(new[]
                {
                    new LOD(0.60f, fallbackArray),
                    new LOD(0.20f, fallbackArray),
                    new LOD(0.05f, fallbackArray),
                });
                fallbackGroup.RecalculateBounds();

                message = $"LODGroup configured with fallback renderers (LOD0={lod0.Count}, LOD1={lod1.Count}, LOD2={lod2.Count}, Fallback={fallbackRenderers.Count}).";
                return true;
            }

            var lodGroup = prefabRoot.GetComponent<LODGroup>();
            if (lodGroup == null)
                lodGroup = prefabRoot.AddComponent<LODGroup>();

            lodGroup.fadeMode = LODFadeMode.None;
            lodGroup.animateCrossFading = false;
            lodGroup.SetLODs(new[]
            {
                new LOD(0.60f, lod0.ToArray()),
                new LOD(0.20f, lod1.ToArray()),
                new LOD(0.05f, lod2.ToArray()),
            });
            lodGroup.RecalculateBounds();

            message = $"LODGroup configured on prefab root with 3 LODs (LOD0={lod0.Count}, LOD1={lod1.Count}, LOD2={lod2.Count}).";
            return true;
        }

        private static void SetupSingleRootCollider(GameObject prefabRoot, GameObject modelRoot, string assetName, string variationName)
        {
            var existing = prefabRoot.GetComponent<Collider>();
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);

            bool isTrigger;
            var colFilter = FindColliderMeshFilter(modelRoot, assetName, preferTrigger: true, out isTrigger);
            if (colFilter != null && colFilter.sharedMesh != null)
            {
                var baked = BakeMeshToLocalSpace(colFilter.sharedMesh, colFilter.transform, prefabRoot.transform);
                string suffix = isTrigger ? "TRIG" : "COL";
                string assetPath = $"{VariationGeneratedRoot}/{variationName}/{assetName}_{suffix}_Baked.asset";

                if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null)
                    AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.CreateAsset(baked, assetPath);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                var bakedAsset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                var mc = prefabRoot.AddComponent<MeshCollider>();
                mc.sharedMesh = bakedAsset;
                mc.convex = false;
                mc.isTrigger = isTrigger;
                return;
            }

            var bounds = ComputeRendererBounds(modelRoot);
            var bc = prefabRoot.AddComponent<BoxCollider>();
            bc.center = prefabRoot.transform.InverseTransformPoint(bounds.center);
            bc.size = bounds.size;
        }

        private static MeshFilter FindColliderMeshFilter(GameObject modelRoot, string assetName, bool preferTrigger, out bool isTrigger)
        {
            string trigName = $"{assetName}_TRIG";
            string colName = $"{assetName}_COL";

            MeshFilter trig = null;
            MeshFilter col = null;

            foreach (var mf in modelRoot.GetComponentsInChildren<MeshFilter>(includeInactive: true))
            {
                if (mf == null || mf.sharedMesh == null)
                    continue;

                string n = mf.gameObject.name;
                if (trig == null && string.Equals(n, trigName, StringComparison.OrdinalIgnoreCase))
                    trig = mf;
                if (col == null && string.Equals(n, colName, StringComparison.OrdinalIgnoreCase))
                    col = mf;
            }

            if (preferTrigger && trig != null)
            {
                isTrigger = true;
                return trig;
            }

            if (col != null)
            {
                isTrigger = false;
                return col;
            }

            isTrigger = false;
            return trig;
        }

        private static Mesh BakeMeshToLocalSpace(Mesh srcMesh, Transform srcTransform, Transform dstTransform)
        {
            var baked = UnityEngine.Object.Instantiate(srcMesh);
            baked.name = $"{srcMesh.name}_Baked";

            Vector3[] verts = srcMesh.vertices;
            var outVerts = new Vector3[verts.Length];

            Matrix4x4 m = dstTransform.worldToLocalMatrix * srcTransform.localToWorldMatrix;
            for (int i = 0; i < verts.Length; i++)
                outVerts[i] = m.MultiplyPoint3x4(verts[i]);

            baked.vertices = outVerts;
            baked.RecalculateBounds();
            return baked;
        }

        private static Bounds ComputeRendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null || renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.one);

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private static void StripChildColliders(GameObject prefabRoot)
        {
            var all = prefabRoot.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null) continue;
                if (ReferenceEquals(c.gameObject, prefabRoot))
                    continue;
                UnityEngine.Object.DestroyImmediate(c);
            }
        }

        private static void DisableColliderMeshRendering(GameObject modelRoot, string assetName)
        {
            string colName = $"{assetName}_COL";
            string trigName = $"{assetName}_TRIG";
            foreach (var r in modelRoot.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                if (r == null) continue;

                string n = r.gameObject.name;
                if (!string.Equals(n, colName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(n, trigName, StringComparison.OrdinalIgnoreCase))
                    continue;

                r.enabled = false;
            }
        }

        private static void ConfigureResourceNode(GameObject prefabRoot, string variationName)
        {
            if (prefabRoot == null)
                return;

            var resourceNode = prefabRoot.GetComponent<ResourceNode>();
            if (resourceNode == null)
                resourceNode = prefabRoot.AddComponent<ResourceNode>();

            var harvestConfig = ResolveHarvestConfig(variationName);
            var woodItem = AssetDatabase.LoadAssetAtPath<Item>(WoodItemAssetPath);
            if (woodItem == null)
            {
                Debug.LogWarning($"[LargeTreeVariationPrefabGenerator] Wood item not found at '{WoodItemAssetPath}'. Variation '{variationName}' will drop nothing until item generation runs.");
            }

            var so = new SerializedObject(resourceNode);
            so.FindProperty("_maxHealth").floatValue = harvestConfig.MaxHealth;
            so.FindProperty("_dropAmount").intValue = harvestConfig.DropAmount;
            so.FindProperty("_dropItem").objectReferenceValue = woodItem;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[LargeTreeVariationPrefabGenerator] Harvest profile '{variationName}' -> health={harvestConfig.MaxHealth:0} drop={harvestConfig.DropAmount}.");
        }

        private static VariationHarvestConfig ResolveHarvestConfig(string variationName)
        {
            if (string.Equals(variationName, "Seedling", StringComparison.OrdinalIgnoreCase))
                return new VariationHarvestConfig(22f, 1);

            if (string.Equals(variationName, "Sapling", StringComparison.OrdinalIgnoreCase))
                return new VariationHarvestConfig(38f, 2);

            if (string.Equals(variationName, "Young", StringComparison.OrdinalIgnoreCase))
                return new VariationHarvestConfig(64f, 4);

            if (string.Equals(variationName, "Mature", StringComparison.OrdinalIgnoreCase))
                return new VariationHarvestConfig(104f, 7);

            if (string.Equals(variationName, "Adult", StringComparison.OrdinalIgnoreCase))
                return new VariationHarvestConfig(160f, 11);

            return new VariationHarvestConfig(80f, 5);
        }

        private static VariationVisualConfig ResolveVisualConfig(string variationName)
        {
            if (string.Equals(variationName, "Seedling", StringComparison.OrdinalIgnoreCase))
                return new VariationVisualConfig(
                    uniformScale: 0.42f,
                    trunkWidthScale: 0.50f,
                    trunkTopScale: 0.35f,
                    trunkHeightScale: 0.88f,
                    trunkLean: 0.00f,
                    canopyRadialScale: 0.45f,
                    canopyVerticalScale: 0.62f,
                    canopyTopPruneStrength: 0.50f,
                    canopyYOffset: -0.18f);

            if (string.Equals(variationName, "Sapling", StringComparison.OrdinalIgnoreCase))
                return new VariationVisualConfig(
                    uniformScale: 0.62f,
                    trunkWidthScale: 0.58f,
                    trunkTopScale: 0.38f,
                    trunkHeightScale: 0.88f,
                    trunkLean: 0.01f,
                    canopyRadialScale: 1.18f,
                    canopyVerticalScale: 0.78f,
                    canopyTopPruneStrength: 0.35f,
                    canopyYOffset: -0.08f);

            if (string.Equals(variationName, "Young", StringComparison.OrdinalIgnoreCase))
                return new VariationVisualConfig(
                    uniformScale: 0.84f,
                    trunkWidthScale: 0.86f,
                    trunkTopScale: 0.72f,
                    trunkHeightScale: 1.00f,
                    trunkLean: 0.02f,
                    canopyRadialScale: 1.10f,
                    canopyVerticalScale: 0.95f,
                    canopyTopPruneStrength: 0.18f,
                    canopyYOffset: 0.00f);

            if (string.Equals(variationName, "Mature", StringComparison.OrdinalIgnoreCase))
                return new VariationVisualConfig(
                    uniformScale: 1.06f,
                    trunkWidthScale: 0.82f,
                    trunkTopScale: 0.74f,
                    trunkHeightScale: 1.00f,
                    trunkLean: 0.03f,
                    canopyRadialScale: 1.42f,
                    canopyVerticalScale: 0.96f,
                    canopyTopPruneStrength: 0.08f,
                    canopyYOffset: 0.08f);

            if (string.Equals(variationName, "Adult", StringComparison.OrdinalIgnoreCase))
                return new VariationVisualConfig(
                    uniformScale: 1.30f,
                    trunkWidthScale: 0.74f,
                    trunkTopScale: 0.66f,
                    trunkHeightScale: 0.90f,
                    trunkLean: 0.04f,
                    canopyRadialScale: 2.00f,
                    canopyVerticalScale: 0.92f,
                    canopyTopPruneStrength: 0.00f,
                    canopyYOffset: 0.12f);

            return new VariationVisualConfig(
                uniformScale: 1.0f,
                trunkWidthScale: 1.0f,
                trunkTopScale: 1.0f,
                trunkHeightScale: 1.0f,
                trunkLean: 0.0f,
                canopyRadialScale: 1.0f,
                canopyVerticalScale: 1.0f,
                canopyTopPruneStrength: 0.0f,
                canopyYOffset: 0.0f);
        }

        private static bool TryApplyStageSilhouette(
            GameObject modelRoot,
            string variationName,
            string assetName,
            VariationVisualConfig visualConfig,
            out string message)
        {
            int sculptedMeshes = 0;
            float lod0Aspect = -1f;
            float lod0CanopyToTrunkRatio = -1f;

            var meshFilters = modelRoot.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                var meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                string objectName = meshFilter.gameObject.name;
                if (string.Equals(objectName, $"{assetName}_COL", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(objectName, $"{assetName}_TRIG", StringComparison.OrdinalIgnoreCase))
                    continue;

                var renderer = meshFilter.GetComponent<Renderer>();
                if (renderer == null)
                    continue;

                int[] barkMaterialSlots;
                int[] leafMaterialSlots;
                ResolveTreeMaterialSlots(renderer.sharedMaterials, out barkMaterialSlots, out leafMaterialSlots);

                bool[] barkMask = BuildVertexMask(meshFilter.sharedMesh, barkMaterialSlots);
                bool[] leafMask = BuildVertexMask(meshFilter.sharedMesh, leafMaterialSlots);

                if (!HasAnyVertexAssignment(barkMask) || !HasAnyVertexAssignment(leafMask))
                    continue;

                var sculptedMesh = UnityEngine.Object.Instantiate(meshFilter.sharedMesh);
                sculptedMesh.name = $"{meshFilter.sharedMesh.name}_{variationName}_Silhouette";

                ApplyGrowthStageSculpt(sculptedMesh, barkMask, leafMask, visualConfig);

                string token = SanitizeAssetToken(meshFilter.gameObject.name);
                string meshPath = $"{VariationGeneratedRoot}/{variationName}/{assetName}_{token}_Silhouette.asset";

                if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null)
                    AssetDatabase.DeleteAsset(meshPath);

                AssetDatabase.CreateAsset(sculptedMesh, meshPath);
                AssetDatabase.ImportAsset(meshPath, ImportAssetOptions.ForceUpdate);

                var persistedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (persistedMesh == null)
                {
                    message = $"Failed to persist sculpted mesh '{meshPath}'.";
                    return false;
                }

                meshFilter.sharedMesh = persistedMesh;
                sculptedMeshes++;

                if (objectName.IndexOf("LOD0", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Bounds b = persistedMesh.bounds;
                    lod0Aspect = b.size.x / Mathf.Max(0.001f, b.size.y);
                    lod0CanopyToTrunkRatio = ComputeCanopyToTrunkWidthRatio(persistedMesh.vertices, barkMask, leafMask);
                }
            }

            if (sculptedMeshes == 0)
            {
                message = "No sculptable tree meshes were found (expected bark + leaves material slots).";
                return false;
            }

            message = $"Applied growth silhouette to {sculptedMeshes} mesh(es). LOD0 aspect={lod0Aspect:0.000}, canopyToTrunk={lod0CanopyToTrunkRatio:0.000}.";
            return true;
        }

        private static void ResolveTreeMaterialSlots(Material[] materials, out int[] barkSlots, out int[] leafSlots)
        {
            var bark = new List<int>();
            var leaf = new List<int>();

            if (materials != null)
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    string name = materials[i] != null ? materials[i].name : string.Empty;
                    if (IsLeavesMaterialName(name))
                    {
                        leaf.Add(i);
                        continue;
                    }

                    if (IsBarkMaterialName(name))
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

        private static bool HasAnyVertexAssignment(bool[] mask)
        {
            if (mask == null)
                return false;

            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i])
                    return true;
            }

            return false;
        }

        private static void ApplyGrowthStageSculpt(Mesh mesh, bool[] barkMask, bool[] leafMask, VariationVisualConfig visualConfig)
        {
            var vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0)
                return;

            Vector3 min = vertices[0];
            Vector3 max = vertices[0];
            for (int i = 1; i < vertices.Length; i++)
            {
                min = Vector3.Min(min, vertices[i]);
                max = Vector3.Max(max, vertices[i]);
            }

            float height = Mathf.Max(0.001f, max.y - min.y);
            float canopyBaseY = min.y + height * 0.33f;
            Vector3 pivot = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);

            // Detect Z-up meshes: preserve the Z extent (world height after rotation)
            // by excluding Z from radial/width operations and letting uniform scale alone
            // control height progression.
            float yExtent = max.y - min.y;
            float zExtent = max.z - min.z;
            bool preserveZHeight = zExtent > yExtent * 1.2f;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                Vector3 relative = v - pivot;

                if (barkMask != null && i < barkMask.Length && barkMask[i])
                {
                    float y01 = Mathf.Clamp01((v.y - min.y) / height);
                    float topTaper = Mathf.Lerp(1f, visualConfig.TrunkTopScale, y01);
                    float widthScale = visualConfig.TrunkWidthScale * topTaper;

                    relative.x *= widthScale;
                    if (!preserveZHeight)
                        relative.z *= widthScale;
                    relative.y *= visualConfig.TrunkHeightScale;
                    relative.x += visualConfig.TrunkLean * y01 * y01;

                    v = pivot + relative;
                }

                if (leafMask != null && i < leafMask.Length && leafMask[i])
                {
                    float canopyHeight = Mathf.Max(0.001f, max.y - canopyBaseY);
                    float canopy01 = Mathf.Clamp01((v.y - canopyBaseY) / canopyHeight);
                    float canopyBlend = canopy01 * canopy01;
                    float topPrune = 1f - visualConfig.CanopyTopPruneStrength * canopy01;

                    relative = v - pivot;
                    float radialScale = Mathf.Lerp(1f, visualConfig.CanopyRadialScale, canopyBlend) * topPrune;
                    relative.x *= radialScale;
                    if (!preserveZHeight)
                        relative.z *= radialScale;
                    relative.y *= Mathf.Lerp(1f, visualConfig.CanopyVerticalScale, canopyBlend);

                    // Clamp the Y offset for Z-up meshes to avoid pushing canopy
                    // vertices beyond the mesh Y extent, which would distort
                    // world-space bounds after the upright rotation.
                    float canopyYOffset = visualConfig.CanopyYOffset;
                    if (preserveZHeight)
                        canopyYOffset = Mathf.Clamp(canopyYOffset, -yExtent * 0.5f, yExtent * 0.5f);
                    relative.y += canopyYOffset * canopyBlend;

                    v = pivot + relative;
                }

                vertices[i] = v;
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
        }

        private static float ComputeCanopyToTrunkWidthRatio(Vector3[] vertices, bool[] barkMask, bool[] leafMask)
        {
            float trunkWidth = ComputeMaskedWidth(vertices, barkMask);
            float canopyWidth = ComputeMaskedWidth(vertices, leafMask);
            return canopyWidth / Mathf.Max(0.001f, trunkWidth);
        }

        private static void NormalizeToTargetWorldHeight(Transform visualRoot, GameObject modelRoot, string variationName)
        {
            if (visualRoot == null || modelRoot == null)
                return;

            Vector3 beforeSize = ComputeRenderableBoundsSize(modelRoot);
            float measuredHeight = Mathf.Max(beforeSize.y, beforeSize.z);
            if (measuredHeight <= 0.001f)
            {
                Debug.LogWarning($"[LargeTreeVariationPrefabGenerator] {variationName}: unable to normalize tree height (measuredHeight={measuredHeight:0.000}).");
                return;
            }

            float targetHeight = ResolveTargetWorldHeight(variationName);
            float multiplier = Mathf.Clamp(targetHeight / measuredHeight, 0.2f, 120f);
            visualRoot.localScale = visualRoot.localScale * multiplier;

            Vector3 afterSize = ComputeRenderableBoundsSize(modelRoot);
            float normalizedHeight = Mathf.Max(afterSize.y, afterSize.z);
            Debug.Log($"[LargeTreeVariationPrefabGenerator] {variationName}: normalized world height {measuredHeight:0.00}m -> {normalizedHeight:0.00}m (target={targetHeight:0.00}m, x{multiplier:0.00}).");
        }

        private static float ResolveTargetWorldHeight(string variationName)
        {
            if (string.Equals(variationName, "Seedling", StringComparison.OrdinalIgnoreCase))
                return 1.4f;
            if (string.Equals(variationName, "Sapling", StringComparison.OrdinalIgnoreCase))
                return 2.3f;
            if (string.Equals(variationName, "Young", StringComparison.OrdinalIgnoreCase))
                return 3.6f;
            if (string.Equals(variationName, "Mature", StringComparison.OrdinalIgnoreCase))
                return 5.2f;
            if (string.Equals(variationName, "Adult", StringComparison.OrdinalIgnoreCase))
                return 7.1f;

            return 4.0f;
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

        private static bool IsBarkMaterialName(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
                return false;

            string n = materialName.ToLowerInvariant();
            return n.Contains("bark") || n.Contains("trunk") || n.Contains("wood");
        }

        private static bool IsLeavesMaterialName(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
                return false;

            string n = materialName.ToLowerInvariant();
            return n.Contains("leaf") || n.Contains("leaves") || n.Contains("foliage") || n.Contains("canopy");
        }

        private static string SanitizeAssetToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return "Mesh";

            var chars = token.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                bool allowed = char.IsLetterOrDigit(chars[i]) || chars[i] == '_' || chars[i] == '-';
                if (!allowed)
                    chars[i] = '_';
            }

            return new string(chars);
        }
    }
}