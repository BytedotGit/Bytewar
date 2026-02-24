using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ByteWar.Editor
{
    public static class BlenderE2EPropPrefabGenerator
    {
        internal const string AssetName = BlenderFbxPostprocessor.E2EAssetName;
        internal const string FbxPath = BlenderFbxPostprocessor.E2EFbxPath;

        internal const string ResourcesFolder = "Assets/Resources";
        internal const string GeneratedFolder = "Assets/Resources/Generated";
        internal const string GeneratedAssetFolder = "Assets/Resources/Generated/BlenderE2EProp";

        internal const string PrefabPath = "Assets/Resources/Generated/BlenderE2EProp/BlenderE2EProp.prefab";
        internal const string ResourcesLoadPath = "Generated/BlenderE2EProp/BlenderE2EProp";

        public static bool Generate()
        {
            // Ensure any updated import settings/postprocessors have run.
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (model == null)
            {
                Debug.Log($"[BlenderE2EPropPrefabGenerator] FBX missing at '{FbxPath}'. Skipping prefab generation.");
                return false;
            }

            EnsureFolder("Assets", "Resources");
            EnsureFolder(ResourcesFolder, "Generated");
            EnsureFolder(GeneratedFolder, "BlenderE2EProp");

            TryCopyPreviewIntoResources();

            var root = new GameObject(AssetName);
            try
            {
                var visualRoot = new GameObject("VisualRoot");
                visualRoot.transform.SetParent(root.transform, worldPositionStays: false);

                var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                modelInstance.name = AssetName;
                modelInstance.transform.SetParent(visualRoot.transform, worldPositionStays: false);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;

                // Ensure the model subtree does not keep an LODGroup; the prefab root owns it.
                var modelGroups = modelInstance.GetComponentsInChildren<LODGroup>(includeInactive: true);
                if (modelGroups != null && modelGroups.Length > 0)
                {
                    for (int i = 0; i < modelGroups.Length; i++)
                    {
                        if (modelGroups[i] != null)
                            UnityEngine.Object.DestroyImmediate(modelGroups[i]);
                    }
                }

                // LODGroup on the prefab root, wired to renderers under the model instance.
                if (!TrySetupLods(root, modelInstance, out string lodMsg))
                    Debug.LogWarning($"[BlenderE2EPropPrefabGenerator] {lodMsg}");
                else
                    Debug.Log($"[BlenderE2EPropPrefabGenerator] {lodMsg}");

                // Collider on root (single), preferably driven by the *_COL mesh.
                SetupSingleRootCollider(root, modelInstance);

                // Strip any child colliders (visuals should not carry colliders).
                StripChildColliders(root);

                // Ensure collider mesh does not render.
                DisableColliderMeshRendering(modelInstance);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.ImportAsset(PrefabPath);

                // Ensure preview texture is importable at runtime.
                TryCopyPreviewIntoResources();

                // Keep the deploy UI catalog in sync with Generated/ resources.
                DeployableAssetCatalogBuilder.TryRegenerate(out _);
                Debug.Log($"[BlenderE2EPropPrefabGenerator] Prefab generated: {PrefabPath}");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void TryCopyPreviewIntoResources()
        {
            const string artPreviewPath = "Assets/Art/Environment/Props/BlenderE2EProp/Preview.png";
            string resourcesPreviewPath = $"{GeneratedAssetFolder}/Preview.png";

            try
            {
                if (!File.Exists(artPreviewPath))
                    return;

                File.Copy(artPreviewPath, resourcesPreviewPath, overwrite: true);
                AssetDatabase.ImportAsset(resourcesPreviewPath, ImportAssetOptions.ForceUpdate);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BlenderE2EPropPrefabGenerator] Failed to copy preview into Resources: {ex.Message}");
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
                message = $"LODGroup not configured: missing renderers (LOD0={lod0.Count}, LOD1={lod1.Count}, LOD2={lod2.Count}).";
                return false;
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

        private static void SetupSingleRootCollider(GameObject prefabRoot, GameObject modelRoot)
        {
            var existing = prefabRoot.GetComponent<Collider>();
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);

            // Prefer trigger volumes when present (non-solid assets like water), otherwise use the solid collider mesh.
            bool isTrigger;
            var colFilter = FindColliderMeshFilter(modelRoot, preferTrigger: true, out isTrigger);
            if (colFilter != null && colFilter.sharedMesh != null)
            {
                var baked = BakeMeshToLocalSpace(colFilter.sharedMesh, colFilter.transform, prefabRoot.transform);
                string assetPath = isTrigger
                    ? $"{GeneratedAssetFolder}/{AssetName}_TRIG_Baked.asset"
                    : $"{GeneratedAssetFolder}/{AssetName}_COL_Baked.asset";

                // Replace deterministically if it already exists.
                if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null)
                    AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.CreateAsset(baked, assetPath);
                AssetDatabase.ImportAsset(assetPath);

                var bakedAsset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                var mc = prefabRoot.AddComponent<MeshCollider>();
                mc.sharedMesh = bakedAsset;
                mc.convex = false;
                mc.isTrigger = isTrigger;
                Debug.Log($"[BlenderE2EPropPrefabGenerator] Root MeshCollider baked from '{colFilter.gameObject.name}'. isTrigger={isTrigger} asset='{assetPath}'.");
                return;
            }

            // Fallback: bounds-based BoxCollider.
            var bounds = ComputeRendererBounds(modelRoot);
            var bc = prefabRoot.AddComponent<BoxCollider>();
            bc.center = prefabRoot.transform.InverseTransformPoint(bounds.center);
            bc.size = bounds.size;
            Debug.Log("[BlenderE2EPropPrefabGenerator] Root BoxCollider configured from renderer bounds (fallback).");
        }

        private static MeshFilter FindColliderMeshFilter(GameObject modelRoot, bool preferTrigger, out bool isTrigger)
        {
            // Convention:
            // - <AssetName>_TRIG => trigger volume (non-solid)
            // - <AssetName>_COL  => solid collider mesh
            string trigName = $"{AssetName}_TRIG";
            string colName = $"{AssetName}_COL";

            MeshFilter trig = null;
            MeshFilter col = null;

            foreach (var mf in modelRoot.GetComponentsInChildren<MeshFilter>(includeInactive: true))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                string n = mf.gameObject.name;
                if (trig == null && string.Equals(n, trigName, StringComparison.OrdinalIgnoreCase)) trig = mf;
                if (col == null && string.Equals(n, colName, StringComparison.OrdinalIgnoreCase)) col = mf;
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
            // MeshCollider vertices are interpreted in the local space of the GameObject holding the collider.
            // If the source collider mesh lives under a child with local offsets, we must bake that transform
            // into the vertex positions so the root collider matches the visible mesh.
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

        private static void DisableColliderMeshRendering(GameObject modelRoot)
        {
            string colName = $"{AssetName}_COL";
            string trigName = $"{AssetName}_TRIG";
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
    }
}
