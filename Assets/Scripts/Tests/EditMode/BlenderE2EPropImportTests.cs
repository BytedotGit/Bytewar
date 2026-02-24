using System.IO;
using ByteWar.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ByteWar.Tests.EditMode
{
    public class BlenderE2EPropImportTests
    {
        private const string FbxPath = "Assets/Art/Environment/Props/BlenderE2EProp/BlenderE2EProp.fbx";
        private const string PrefabPath = "Assets/Resources/Generated/BlenderE2EProp/BlenderE2EProp.prefab";
        private const string PreviewPngPath = "Assets/Art/Environment/Props/BlenderE2EProp/Preview.png";

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

                Assert.Greater(lod0Tris, lod1Tris, "Expected LOD0 triangles > LOD1 triangles.");
                Assert.Greater(lod1Tris, lod2Tris, "Expected LOD1 triangles > LOD2 triangles.");

                var colliders = instance.GetComponentsInChildren<Collider>(includeInactive: true);
                Assert.AreEqual(1, colliders.Length, "Expected exactly 1 collider in the prefab hierarchy.");
                Assert.AreSame(instance, colliders[0].gameObject, "Expected the single collider to be on the prefab root.");

                Assert.IsFalse(colliders[0].isTrigger, "Expected BlenderE2EProp collider to be solid (isTrigger=false).");

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
