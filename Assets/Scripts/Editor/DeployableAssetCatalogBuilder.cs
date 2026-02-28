using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using ByteWar.UI;

namespace ByteWar.Editor
{
    public static class DeployableAssetCatalogBuilder
    {
        internal const string CatalogAssetPath = "Assets/Resources/Generated/DeployableAssetCatalog.asset";
        internal const string CatalogResourcesLoadPath = "Generated/DeployableAssetCatalog";

        private const string ResourcesRoot = "Assets/Resources/";
        private const string GeneratedRoot = "Assets/Resources/Generated";
        private const string QuaterniusGeneratedRoot = "Assets/Resources/Generated/Quaternius";
        private const string QuaterniusNatureSourceRoot = "Assets/Art/Premade/Quaternius/UltimateNature/FBX";
        private const string QuaterniusCropsSourceRoot = "Assets/Art/Premade/Quaternius/UltimateCrops/FBX";

        [MenuItem("Tools/ByteWar/Regenerate Deployable Asset Catalog")]
        public static void RegenerateFromMenu()
        {
            bool ok = TryRegenerate(out string msg);
            if (ok) Debug.Log($"[DeployableAssetCatalogBuilder] {msg}");
            else Debug.LogWarning($"[DeployableAssetCatalogBuilder] {msg}");
        }

        public static bool TryRegenerate(out string message)
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "Generated");

            if (!TryGenerateQuaterniusDeployablePrefabs(out int generatedQuaterniusCount, out string generationMessage))
            {
                message = generationMessage;
                return false;
            }

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { GeneratedRoot });
            var entries = new List<DeployableAssetCatalog.Entry>();

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrWhiteSpace(assetPath)) continue;
                if (!assetPath.StartsWith(ResourcesRoot, StringComparison.OrdinalIgnoreCase)) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) continue;

                // Keep the deploy UI aligned with server-side placement validation.
                if (prefab.GetComponent<NetworkObject>() == null) continue;
                if (prefab.GetComponentInChildren<LODGroup>(true) == null) continue;
                if (prefab.GetComponentInChildren<Collider>(true) == null) continue;

                string resourcePath = ToResourcesLoadPath(assetPath);
                if (string.IsNullOrEmpty(resourcePath)) continue;

                string previewResourcePath = FindPreviewResourcePath(assetPath);
                entries.Add(new DeployableAssetCatalog.Entry(prefab.name, resourcePath, previewResourcePath));
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.ResourcePath, b.ResourcePath));

            var catalog = AssetDatabase.LoadAssetAtPath<DeployableAssetCatalog>(CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DeployableAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            catalog.SetEntries(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(CatalogAssetPath);

            message = $"Catalog regenerated with {entries.Count} entry(s), Quaternius generated={generatedQuaterniusCount}: {CatalogAssetPath}";
            return true;
        }

        private static bool TryGenerateQuaterniusDeployablePrefabs(out int generatedCount, out string message)
        {
            generatedCount = 0;
            message = string.Empty;

            if (!AssetDatabase.IsValidFolder(QuaterniusNatureSourceRoot) && !AssetDatabase.IsValidFolder(QuaterniusCropsSourceRoot))
            {
                message = "Quaternius source roots missing; continuing without generated Quaternius deployables.";
                return true;
            }

            var sourceGuids = new List<string>();
            if (AssetDatabase.IsValidFolder(QuaterniusNatureSourceRoot))
                sourceGuids.AddRange(AssetDatabase.FindAssets("t:GameObject", new[] { QuaterniusNatureSourceRoot }));
            if (AssetDatabase.IsValidFolder(QuaterniusCropsSourceRoot))
                sourceGuids.AddRange(AssetDatabase.FindAssets("t:GameObject", new[] { QuaterniusCropsSourceRoot }));

            sourceGuids.Sort(StringComparer.Ordinal);

            for (int i = 0; i < sourceGuids.Count; i++)
            {
                string sourcePath = AssetDatabase.GUIDToAssetPath(sourceGuids[i]);
                if (string.IsNullOrWhiteSpace(sourcePath) || !sourcePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    continue;

                string prefabPath = BuildQuaterniusGeneratedPrefabPath(sourcePath);
                if (string.IsNullOrWhiteSpace(prefabPath))
                    continue;

                GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (!TryHasRenderable(sourceModel))
                    continue;

                EnsureFolderForAssetPath(prefabPath);

                var root = CreateQuaterniusDeployableRoot(sourceModel);
                if (root == null)
                    continue;

                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                    AssetDatabase.DeleteAsset(prefabPath);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                generatedCount++;
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            message = $"Quaternius deployable generation complete. prefabs={generatedCount}";
            return true;
        }

        private static string BuildQuaterniusGeneratedPrefabPath(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                return string.Empty;

            string family;
            string relative;
            if (sourcePath.StartsWith(QuaterniusNatureSourceRoot, StringComparison.OrdinalIgnoreCase))
            {
                family = "UltimateNature";
                relative = sourcePath.Substring(QuaterniusNatureSourceRoot.Length).TrimStart('/', '\\');
            }
            else if (sourcePath.StartsWith(QuaterniusCropsSourceRoot, StringComparison.OrdinalIgnoreCase))
            {
                family = "UltimateCrops";
                relative = sourcePath.Substring(QuaterniusCropsSourceRoot.Length).TrimStart('/', '\\');
            }
            else
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(relative))
                return string.Empty;

            string relativeWithoutExt = Path.ChangeExtension(relative, null)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(relativeWithoutExt))
                return string.Empty;

            return $"{QuaterniusGeneratedRoot}/{family}/{relativeWithoutExt}.prefab";
        }

        private static GameObject CreateQuaterniusDeployableRoot(GameObject sourceModel)
        {
            if (sourceModel == null)
                return null;

            var root = new GameObject(sourceModel.name);
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();

            var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(sourceModel);
            if (modelInstance == null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                return null;
            }

            modelInstance.name = sourceModel.name;
            modelInstance.transform.SetParent(root.transform, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localScale = Vector3.one;

            Quaternion correction = ResolveUprightRotation(ComputeRenderableBoundsSize(modelInstance));
            modelInstance.transform.localRotation = correction * modelInstance.transform.localRotation;

            if (!TryConfigureLodGroup(root))
            {
                UnityEngine.Object.DestroyImmediate(root);
                return null;
            }

            if (!TryConfigureRootCollider(root))
            {
                UnityEngine.Object.DestroyImmediate(root);
                return null;
            }

            return root;
        }

        private static bool TryConfigureLodGroup(GameObject root)
        {
            if (root == null)
                return false;

            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null || renderers.Length == 0)
                return false;

            var lodGroup = root.GetComponent<LODGroup>();
            if (lodGroup == null)
                lodGroup = root.AddComponent<LODGroup>();

            lodGroup.SetLODs(new[]
            {
                new LOD(0.60f, renderers),
                new LOD(0.25f, renderers),
                new LOD(0.01f, renderers),
            });
            lodGroup.fadeMode = LODFadeMode.None;
            lodGroup.RecalculateBounds();
            return true;
        }

        private static bool TryConfigureRootCollider(GameObject root)
        {
            if (root == null)
                return false;

            if (!TryGetRenderableBounds(root, out Bounds bounds))
                return false;

            var collider = root.GetComponent<BoxCollider>();
            if (collider == null)
                collider = root.AddComponent<BoxCollider>();

            Vector3 localCenter = root.transform.InverseTransformPoint(bounds.center);
            collider.center = localCenter;
            collider.size = new Vector3(
                Mathf.Max(bounds.size.x, 0.05f),
                Mathf.Max(bounds.size.y, 0.05f),
                Mathf.Max(bounds.size.z, 0.05f));
            collider.isTrigger = false;
            return true;
        }

        private static Vector3 ComputeRenderableBoundsSize(GameObject root)
        {
            if (!TryGetRenderableBounds(root, out Bounds bounds))
                return Vector3.zero;

            return bounds.size;
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

        private static Quaternion ResolveUprightRotation(Vector3 renderableSize)
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

        private static bool TryHasRenderable(GameObject root)
        {
            if (root == null)
                return false;

            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            return renderers != null && renderers.Length > 0;
        }

        private static void EnsureFolderForAssetPath(string assetPath)
        {
            string dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(dir) || AssetDatabase.IsValidFolder(dir))
                return;

            string[] parts = dir.Split('/');
            if (parts.Length < 2)
                return;

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string ToResourcesLoadPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return string.Empty;
            if (!assetPath.StartsWith(ResourcesRoot, StringComparison.OrdinalIgnoreCase)) return string.Empty;

            string relative = assetPath.Substring(ResourcesRoot.Length);
            string ext = Path.GetExtension(relative);
            if (!string.IsNullOrEmpty(ext))
                relative = relative.Substring(0, relative.Length - ext.Length);

            return relative.Replace('\\', '/');
        }

        private static string FindPreviewResourcePath(string prefabAssetPath)
        {
            // Convention: Preview texture lives in the same Resources folder as the prefab and is named 'Preview'.
            string dir = Path.GetDirectoryName(prefabAssetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(dir)) return string.Empty;

            string previewPng = $"{dir}/Preview.png";
            string previewJpg = $"{dir}/Preview.jpg";
            string previewTga = $"{dir}/Preview.tga";

            string previewAssetPath = File.Exists(previewPng) ? previewPng :
                File.Exists(previewJpg) ? previewJpg :
                File.Exists(previewTga) ? previewTga :
                string.Empty;

            if (string.IsNullOrEmpty(previewAssetPath))
                return string.Empty;

            return ToResourcesLoadPath(previewAssetPath);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = string.IsNullOrEmpty(parent) ? name : $"{parent}/{name}";
            if (AssetDatabase.IsValidFolder(path)) return;

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
    }
}
