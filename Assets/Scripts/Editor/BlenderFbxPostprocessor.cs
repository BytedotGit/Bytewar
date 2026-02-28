using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ByteWar.Editor
{
    public sealed class BlenderFbxPostprocessor : AssetPostprocessor
    {
        internal const string E2EAssetName = "BlenderE2EProp";
        internal const string E2EFbxPath = "Assets/Art/Environment/Props/BlenderE2EProp/BlenderE2EProp.fbx";
        internal const string LargeTreeAssetName = "LargeTree";
        internal const string LargeTreeFbxPath = "Assets/Art/Environment/Vegetation/LargeTree/LargeTree.fbx";

        private void OnPreprocessModel()
        {
            if (!IsTargetBlenderAsset(assetPath))
                return;

            var importer = (ModelImporter)assetImporter;

            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;

            importer.globalScale = 1f;
            importer.useFileScale = true;

            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.normalSmoothingAngle = 60f;

            // We validate UVs/triangles at runtime in AutoTester, so keep meshes readable for this specific artifact.
            importer.isReadable = true;

            importer.addCollider = false;

            // Keep materials deterministic; Standard (BIRP) is the project baseline.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

            Debug.Log($"[BlenderFbxPostprocessor] Preprocess model import settings applied: {assetPath}");
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!IsTargetBlenderAsset(assetPath))
                return;

            if (root == null)
            {
                Debug.LogWarning("[BlenderFbxPostprocessor] Postprocess root is null.");
                return;
            }

            // Important: the generated Resources prefab owns the single authoritative LODGroup.
            // If the imported model also has an LODGroup, Unity will warn that renderers are
            // registered with more than one LODGroup at runtime.
            var groups = root.GetComponentsInChildren<LODGroup>(includeInactive: true);
            if (groups != null && groups.Length > 0)
            {
                for (int i = 0; i < groups.Length; i++)
                {
                    if (groups[i] != null)
                        UnityEngine.Object.DestroyImmediate(groups[i]);
                }
                Debug.Log($"[BlenderFbxPostprocessor] Removed {groups.Length} LODGroup component(s) from imported model '{root.name}'.");
            }
        }

        private static bool IsTargetBlenderAsset(string p)
        {
            return string.Equals(p, E2EFbxPath, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(p, LargeTreeFbxPath, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryBuildLodGroup(GameObject root, out string message)
        {
            if (root == null)
            {
                message = "LODGroup not created: root is null.";
                return false;
            }

            var lod0 = new List<Renderer>();
            var lod1 = new List<Renderer>();
            var lod2 = new List<Renderer>();

            foreach (var tr in root.GetComponentsInChildren<Transform>(includeInactive: true))
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
                message = $"LODGroup not created: missing renderers (LOD0={lod0.Count}, LOD1={lod1.Count}, LOD2={lod2.Count}).";
                return false;
            }

            var lodGroup = root.GetComponent<LODGroup>();
            if (lodGroup == null)
                lodGroup = root.AddComponent<LODGroup>();

            var lods = new[]
            {
                new LOD(0.60f, lod0.ToArray()),
                new LOD(0.20f, lod1.ToArray()),
                new LOD(0.05f, lod2.ToArray()),
            };

            lodGroup.fadeMode = LODFadeMode.None;
            lodGroup.animateCrossFading = false;
            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            message = $"LODGroup configured on '{root.name}' with 3 LODs (LOD0={lod0.Count}, LOD1={lod1.Count}, LOD2={lod2.Count}).";
            return true;
        }
    }
}
