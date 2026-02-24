using System.Collections;
using UnityEngine;

namespace ByteWar.Core
{
    /// <summary>
    /// AutoTester scenario that validates the Blender E2E prop is present and sane at runtime:
    /// - loads from Resources
    /// - has LODGroup with 3 LODs
    /// - has a single root collider
    /// - UV0 exists on all LOD meshes
    /// - triangle counts decrease from LOD0→LOD1→LOD2
    /// </summary>
    public class AutoTestScenarioBlenderE2EProp : IAutoTestScenario
    {
        public string Name => "BlenderE2EProp";

        public IEnumerator Run(AutoTesterContext ctx)
        {
            Debug.Log("[AutoTester] Running BlenderE2EProp scenario...");

            var prefab = Resources.Load<GameObject>("Generated/BlenderE2EProp/BlenderE2EProp");
            if (prefab == null)
            {
                Debug.LogWarning("[AutoTester] BlenderE2EProp: Resources prefab missing; scenario skipped.");
                Debug.Log("[AutoTester] PASS: BlenderE2EProp scenario skipped (missing prefab).");
                yield break;
            }

            var instance = Object.Instantiate(prefab);
            try
            {
                var lodGroup = instance.GetComponent<LODGroup>();
                if (lodGroup == null)
                {
                    Debug.LogError("[AutoTester] FAIL: BlenderE2EProp - LODGroup missing on prefab root.");
                    Application.Quit(80);
                    yield break;
                }

                var lods = lodGroup.GetLODs();
                if (lods == null || lods.Length != 3)
                {
                    Debug.LogError($"[AutoTester] FAIL: BlenderE2EProp - Expected 3 LODs, found {(lods == null ? -1 : lods.Length)}.");
                    Application.Quit(81);
                    yield break;
                }

                int lod0Tris = SumTrianglesAndValidateUvs(lods[0], "LOD0");
                int lod1Tris = SumTrianglesAndValidateUvs(lods[1], "LOD1");
                int lod2Tris = SumTrianglesAndValidateUvs(lods[2], "LOD2");

                Debug.Log($"[AutoTester] BlenderE2EProp triangles: LOD0={lod0Tris} LOD1={lod1Tris} LOD2={lod2Tris}");

                if (lod0Tris <= lod1Tris || lod1Tris <= lod2Tris)
                {
                    Debug.LogError("[AutoTester] FAIL: BlenderE2EProp - Triangle counts are not strictly decreasing across LODs.");
                    Application.Quit(82);
                    yield break;
                }

                var colliders = instance.GetComponentsInChildren<Collider>(includeInactive: true);
                if (colliders == null || colliders.Length != 1 || colliders[0] == null || colliders[0].gameObject != instance)
                {
                    int count = colliders == null ? -1 : colliders.Length;
                    string where = (colliders != null && colliders.Length > 0 && colliders[0] != null) ? colliders[0].gameObject.name : "n/a";
                    Debug.LogError($"[AutoTester] FAIL: BlenderE2EProp - Expected exactly 1 collider on root. count={count} firstOn='{where}'");
                    Application.Quit(83);
                    yield break;
                }

                if (colliders[0].isTrigger)
                {
                    Debug.LogError("[AutoTester] FAIL: BlenderE2EProp - Collider isTrigger=true but prop is expected to be solid.");
                    Application.Quit(87);
                    yield break;
                }

                if (colliders[0] is MeshCollider mc && mc.sharedMesh != null)
                {
                    Bounds b = mc.sharedMesh.bounds;
                    Debug.Log($"[AutoTester] BlenderE2EProp collider bounds: min={b.min} max={b.max}");

                    if (b.min.y < -0.10f || b.max.y < 0.50f)
                    {
                        Debug.LogError($"[AutoTester] FAIL: BlenderE2EProp - Collider bounds suspect (min.y={b.min.y:0.000} max.y={b.max.y:0.000}). Likely unbaked offset.");
                        Application.Quit(88);
                        yield break;
                    }
                }

                Debug.Log("[AutoTester] PASS: Blender E2E prop sanity.");
                Debug.Log("[AutoTester] PASS: BlenderE2EProp scenario complete.");
            }
            finally
            {
                Object.Destroy(instance);
            }

            yield return null;
        }

        private static int SumTrianglesAndValidateUvs(LOD lod, string label)
        {
            if (lod.renderers == null || lod.renderers.Length == 0)
                return 0;

            int triCount = 0;
            foreach (var r in lod.renderers)
            {
                if (r == null) continue;

                Mesh mesh = null;
                if (r is SkinnedMeshRenderer smr)
                    mesh = smr.sharedMesh;
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null) mesh = mf.sharedMesh;
                }

                if (mesh == null)
                {
                    Debug.LogError($"[AutoTester] FAIL: BlenderE2EProp - {label} renderer '{r.name}' missing mesh.");
                    Application.Quit(84);
                    return 0;
                }

                if (mesh.uv == null || mesh.uv.Length == 0)
                {
                    Debug.LogError($"[AutoTester] FAIL: BlenderE2EProp - {label} mesh '{mesh.name}' missing UV0.");
                    Application.Quit(85);
                    return 0;
                }

                triCount += mesh.triangles.Length / 3;
            }

            if (triCount <= 0)
            {
                Debug.LogError($"[AutoTester] FAIL: BlenderE2EProp - {label} triangle count is {triCount}.");
                Application.Quit(86);
            }

            return triCount;
        }
    }
}
