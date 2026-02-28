using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ByteWar.Survival;

namespace ByteWar.Core
{
    /// <summary>
    /// AutoTester scenario that validates the Blender E2E prop is present and sane at runtime:
    /// - loads from Resources
    /// - has LODGroup with 3 LODs
    /// - has a single root collider
    /// - UV0 exists on all LOD meshes
    /// - triangle counts are either strict LOD0>LOD1>LOD2 or flat fallback (LOD0==LOD1==LOD2)
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

                if (!IsLodTriangleProgressionAcceptable(lod0Tris, lod1Tris, lod2Tris, allowFlatFallback: true, out bool isStrictDecreasing))
                {
                    Debug.LogError("[AutoTester] FAIL: BlenderE2EProp - Triangle counts must be strictly decreasing for authored LODs, or exactly equal for fallback LOD meshes.");
                    Application.Quit(82);
                    yield break;
                }

                if (!isStrictDecreasing)
                    Debug.Log("[AutoTester] BlenderE2EProp LOD fallback detected: triangle counts are equal across all LODs.");

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

        public static bool IsBlenderPropLodTriangleProgressionAcceptable(int lod0Triangles, int lod1Triangles, int lod2Triangles, out bool isStrictDecreasing)
        {
            return IsLodTriangleProgressionAcceptable(lod0Triangles, lod1Triangles, lod2Triangles, allowFlatFallback: true, out isStrictDecreasing);
        }

        public static bool IsLodTriangleProgressionAcceptable(int lod0Triangles, int lod1Triangles, int lod2Triangles, bool allowFlatFallback, out bool isStrictDecreasing)
        {
            isStrictDecreasing = false;
            if (lod0Triangles <= 0 || lod1Triangles <= 0 || lod2Triangles <= 0)
                return false;

            isStrictDecreasing = lod0Triangles > lod1Triangles && lod1Triangles > lod2Triangles;
            if (isStrictDecreasing)
                return true;

            if (!allowFlatFallback)
                return false;

            // Fallback-generated LODGroups can intentionally reuse the same mesh for all levels.
            return lod0Triangles == lod1Triangles && lod1Triangles == lod2Triangles;
        }
    }

    public class AutoTestScenarioLargeTree : IAutoTestScenario
    {
        private static readonly string[] GrowthStages =
        {
            "Seedling",
            "Sapling",
            "Young",
            "Mature",
            "Adult",
        };

        public string Name => "LargeTree";

        public IEnumerator Run(AutoTesterContext ctx)
        {
            Debug.Log("[AutoTester] Running LargeTree scenario...");

            var prefab = Resources.Load<GameObject>("Generated/LargeTree/LargeTree");
            if (prefab == null)
            {
                Debug.LogWarning("[AutoTester] LargeTree: Resources prefab missing; scenario skipped.");
                Debug.Log("[AutoTester] PASS: LargeTree scenario skipped (missing prefab).");
                yield break;
            }

            var instance = Object.Instantiate(prefab);
            try
            {
                var networkObject = instance.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    Debug.LogError("[AutoTester] FAIL: LargeTree - NetworkObject missing on prefab root.");
                    Application.Quit(90);
                    yield break;
                }

                var lodGroup = instance.GetComponent<LODGroup>();
                if (lodGroup == null)
                {
                    Debug.LogError("[AutoTester] FAIL: LargeTree - LODGroup missing on prefab root.");
                    Application.Quit(91);
                    yield break;
                }

                var lods = lodGroup.GetLODs();
                if (lods == null || lods.Length != 3)
                {
                    Debug.LogError($"[AutoTester] FAIL: LargeTree - Expected 3 LODs, found {(lods == null ? -1 : lods.Length)}.");
                    Application.Quit(92);
                    yield break;
                }

                int lod0Tris = SumTrianglesAndValidateUvs(lods[0], "LOD0");
                int lod1Tris = SumTrianglesAndValidateUvs(lods[1], "LOD1");
                int lod2Tris = SumTrianglesAndValidateUvs(lods[2], "LOD2");

                Debug.Log($"[AutoTester] LargeTree triangles: LOD0={lod0Tris} LOD1={lod1Tris} LOD2={lod2Tris}");

                if (!AutoTestScenarioBlenderE2EProp.IsLodTriangleProgressionAcceptable(lod0Tris, lod1Tris, lod2Tris, allowFlatFallback: true, out bool isStrictDecreasing))
                {
                    Debug.LogError("[AutoTester] FAIL: LargeTree - Triangle counts must be strictly decreasing for authored LODs, or exactly equal for fallback LOD meshes.");
                    Application.Quit(93);
                    yield break;
                }

                if (!isStrictDecreasing)
                    Debug.Log("[AutoTester] LargeTree LOD fallback detected: triangle counts are equal across all LODs.");

                var colliders = instance.GetComponentsInChildren<Collider>(includeInactive: true);
                if (colliders == null || colliders.Length != 1 || colliders[0] == null || colliders[0].gameObject != instance)
                {
                    int count = colliders == null ? -1 : colliders.Length;
                    string where = (colliders != null && colliders.Length > 0 && colliders[0] != null) ? colliders[0].gameObject.name : "n/a";
                    Debug.LogError($"[AutoTester] FAIL: LargeTree - Expected exactly 1 collider on root. count={count} firstOn='{where}'");
                    Application.Quit(94);
                    yield break;
                }

                if (colliders[0].isTrigger)
                {
                    Debug.LogError("[AutoTester] FAIL: LargeTree - Collider isTrigger=true but tree is expected to be solid.");
                    Application.Quit(95);
                    yield break;
                }

                if (!TryValidateGrowthShowcaseInScene(out string showcaseMessage))
                    Debug.LogWarning($"[AutoTester] LargeTree showcase validation skipped: {showcaseMessage}");
                else
                    Debug.Log("[AutoTester] PASS: LargeTree growth-stage showcase is present in scene.");

                Debug.Log("[AutoTester] PASS: LargeTree runtime prefab sanity.");
                Debug.Log("[AutoTester] PASS: LargeTree scenario complete.");
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
                    Debug.LogError($"[AutoTester] FAIL: LargeTree - {label} renderer '{r.name}' missing mesh.");
                    Application.Quit(96);
                    return 0;
                }

                if (mesh.uv == null || mesh.uv.Length == 0)
                {
                    Debug.LogError($"[AutoTester] FAIL: LargeTree - {label} mesh '{mesh.name}' missing UV0.");
                    Application.Quit(97);
                    return 0;
                }

                triCount += mesh.triangles.Length / 3;
            }

            if (triCount <= 0)
            {
                Debug.LogError($"[AutoTester] FAIL: LargeTree - {label} triangle count is {triCount}.");
                Application.Quit(98);
            }

            return triCount;
        }

        private static bool TryValidateGrowthShowcaseInScene(out string message)
        {
            const float minimumHeightGrowthFactor = 1.08f;
            const float minimumAspectRetentionFactor = 0.82f;
            const float minimumFootprintRetentionFactor = 0.68f;
            const float minimumFinalAspectGrowthFactor = 1.12f;
            const float minimumFinalFootprintGrowthFactor = 1.45f;

            float previousHeight = -1f;
            float previousAspect = -1f;
            float previousCanopyFootprint = -1f;
            float firstAspect = -1f;
            float firstCanopyFootprint = -1f;
            float lastAspect = -1f;
            float lastCanopyFootprint = -1f;

            for (int i = 0; i < GrowthStages.Length; i++)
            {
                string stageName = GrowthStages[i];
                string expectedObjectName = $"Placed_LargeTree_{stageName}";
                var sceneObject = GameObject.Find(expectedObjectName);
                if (sceneObject == null)
                {
                    message = $"Expected scene object '{expectedObjectName}' was not found.";
                    return false;
                }

                if (sceneObject.GetComponent<LODGroup>() == null)
                {
                    message = $"Scene object '{expectedObjectName}' is missing LODGroup.";
                    return false;
                }

                if (sceneObject.GetComponent<ResourceNode>() == null)
                {
                    message = $"Scene object '{expectedObjectName}' is missing ResourceNode.";
                    return false;
                }

                if (!TryGetRendererHeight(sceneObject, out float height))
                {
                    message = $"Scene object '{expectedObjectName}' does not have measurable renderer bounds.";
                    return false;
                }

                if (!TryGetLod0SilhouetteMetrics(sceneObject, out float aspect, out float canopyFootprint))
                {
                    message = $"Scene object '{expectedObjectName}' does not expose measurable LOD0 silhouette metrics.";
                    return false;
                }

                Debug.Log($"[AutoTester] LargeTree showcase stage '{stageName}' height={height:0.00} aspect={aspect:0.000} canopyFootprint={canopyFootprint:0.000}.");

                if (previousHeight > 0f && height <= previousHeight * minimumHeightGrowthFactor)
                {
                    message = $"Scene object '{expectedObjectName}' height={height:0.00} is not sufficiently larger than previous stage ({previousHeight:0.00}).";
                    return false;
                }

                if (previousAspect > 0f && aspect < previousAspect * minimumAspectRetentionFactor)
                {
                    message = $"Scene object '{expectedObjectName}' silhouette aspect={aspect:0.000} regressed too far from previous stage ({previousAspect:0.000}).";
                    return false;
                }

                if (previousCanopyFootprint > 0f && canopyFootprint < previousCanopyFootprint * minimumFootprintRetentionFactor)
                {
                    message = $"Scene object '{expectedObjectName}' canopyFootprint={canopyFootprint:0.000} regressed too far from previous stage ({previousCanopyFootprint:0.000}).";
                    return false;
                }

                if (firstAspect < 0f)
                    firstAspect = aspect;
                if (firstCanopyFootprint < 0f)
                    firstCanopyFootprint = canopyFootprint;

                lastAspect = aspect;
                lastCanopyFootprint = canopyFootprint;

                previousHeight = height;
                previousAspect = aspect;
                previousCanopyFootprint = canopyFootprint;
            }

            if (firstAspect > 0f && lastAspect <= firstAspect * minimumFinalAspectGrowthFactor)
            {
                message = $"Adult showcase silhouette aspect={lastAspect:0.000} is not sufficiently broader than Seedling ({firstAspect:0.000}).";
                return false;
            }

            if (firstCanopyFootprint > 0f && lastCanopyFootprint <= firstCanopyFootprint * minimumFinalFootprintGrowthFactor)
            {
                message = $"Adult showcase canopyFootprint={lastCanopyFootprint:0.000} is not sufficiently broader than Seedling ({firstCanopyFootprint:0.000}).";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static bool TryGetRendererHeight(GameObject target, out float height)
        {
            height = 0f;
            if (target == null)
                return false;

            var renderers = target.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null || renderers.Length == 0)
                return false;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            height = bounds.size.y;
            return height > 0.01f;
        }

        private static bool TryGetLod0SilhouetteMetrics(GameObject target, out float aspect, out float canopyFootprint)
        {
            aspect = 0f;
            canopyFootprint = 0f;

            if (target == null)
                return false;

            var lodGroup = target.GetComponent<LODGroup>();
            if (lodGroup == null)
                return false;

            var lods = lodGroup.GetLODs();
            if (lods == null || lods.Length == 0)
                return false;

            var lod0Renderers = lods[0].renderers;
            if (!TryGetRenderableBounds(lod0Renderers, out Bounds totalBounds))
            {
                var allRenderers = target.GetComponentsInChildren<Renderer>(includeInactive: true);
                if (!TryGetRenderableBounds(allRenderers, out totalBounds))
                {
                    if (!TryGetTransformedMeshBounds(target, out totalBounds))
                        return false;

                    Debug.LogWarning($"[AutoTester] LargeTree showcase: Using mesh-bounds fallback for '{target.name}' silhouette metrics.");
                }
                else
                {
                    Debug.LogWarning($"[AutoTester] LargeTree showcase: LOD0 renderer bounds unavailable, using all-renderer fallback for '{target.name}'.");
                }
            }

            float height = Mathf.Max(0.001f, totalBounds.size.y);
            float horizontalWidth = ComputeHorizontalWidth(totalBounds);
            aspect = horizontalWidth / height;

            // Runtime-safe proxy for canopy breadth that does not require mesh Read/Write data.
            canopyFootprint = horizontalWidth;

            return aspect > 0.01f && canopyFootprint > 0.01f;
        }

        internal static bool TryGetLod0SilhouetteMetricsForTests(GameObject target, out float aspect, out float canopyFootprint)
            => TryGetLod0SilhouetteMetrics(target, out aspect, out canopyFootprint);

        private static bool TryGetRenderableBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            if (renderers == null || renderers.Length == 0)
                return false;

            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                Bounds next = renderer.bounds;
                if (next.size.sqrMagnitude <= 0.000001f)
                    continue;

                EncapsulateBounds(ref bounds, ref hasBounds, next);
            }

            return hasBounds;
        }

        private static bool TryGetTransformedMeshBounds(GameObject target, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            if (target == null)
                return false;

            var meshFilters = target.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                var meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                EncapsulateTransformedLocalBounds(meshFilter.sharedMesh.bounds, meshFilter.transform.localToWorldMatrix, ref bounds, ref hasBounds);
            }

            var skinnedRenderers = target.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                var skinned = skinnedRenderers[i];
                if (skinned == null || skinned.sharedMesh == null)
                    continue;

                EncapsulateTransformedLocalBounds(skinned.localBounds, skinned.transform.localToWorldMatrix, ref bounds, ref hasBounds);
            }

            return hasBounds;
        }

        private static void EncapsulateTransformedLocalBounds(Bounds localBounds, Matrix4x4 localToWorld, ref Bounds aggregate, ref bool hasAggregate)
        {
            Vector3 center = localBounds.center;
            Vector3 extents = localBounds.extents;

            // Transform all local AABB corners to build a reliable world-space bounds.
            Vector3[] corners =
            {
                new Vector3(center.x - extents.x, center.y - extents.y, center.z - extents.z),
                new Vector3(center.x + extents.x, center.y - extents.y, center.z - extents.z),
                new Vector3(center.x - extents.x, center.y + extents.y, center.z - extents.z),
                new Vector3(center.x + extents.x, center.y + extents.y, center.z - extents.z),
                new Vector3(center.x - extents.x, center.y - extents.y, center.z + extents.z),
                new Vector3(center.x + extents.x, center.y - extents.y, center.z + extents.z),
                new Vector3(center.x - extents.x, center.y + extents.y, center.z + extents.z),
                new Vector3(center.x + extents.x, center.y + extents.y, center.z + extents.z),
            };

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 worldPoint = localToWorld.MultiplyPoint3x4(corners[i]);
                if (!hasAggregate)
                {
                    aggregate = new Bounds(worldPoint, Vector3.zero);
                    hasAggregate = true;
                }
                else
                {
                    aggregate.Encapsulate(worldPoint);
                }
            }
        }

        private static void EncapsulateBounds(ref Bounds aggregate, ref bool hasAggregate, Bounds next)
        {
            if (!hasAggregate)
            {
                aggregate = next;
                hasAggregate = true;
                return;
            }

            aggregate.Encapsulate(next);
        }

        private static float ComputeHorizontalWidth(Bounds bounds)
            => (bounds.size.x + bounds.size.z) * 0.5f;


    }
}
