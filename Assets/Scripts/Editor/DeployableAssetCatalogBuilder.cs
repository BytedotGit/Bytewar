using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using Unity.Netcode;
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

            message = $"Catalog regenerated with {entries.Count} entry(s): {CatalogAssetPath}";
            return true;
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
