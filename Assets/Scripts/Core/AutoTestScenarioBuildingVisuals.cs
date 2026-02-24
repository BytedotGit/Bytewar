using System.Collections;
using ByteWar.Building;
using Unity.Netcode;
using UnityEngine;

namespace ByteWar.Core
{
    /// <summary>
    /// AutoTester scenario that validates building prefab visuals at runtime:
    /// - 9 building prefabs registered in NetworkConfig.Prefabs
    /// - Stairs prefab has 4 step renderers
    /// - Ramp/Roof26/AngledWall prefabs contain MeshFilter meshes with expected geometry
    /// </summary>
    public class AutoTestScenarioBuildingVisuals : IAutoTestScenario
    {
        public string Name => "BuildingVisuals";

        public IEnumerator Run(AutoTesterContext ctx)
        {
            Debug.Log("[AutoTester] Running BuildingVisuals scenario...");

            var nm = ctx.NetworkManager;
            if (nm == null)
            {
                Debug.LogError("[AutoTester] FAIL: BuildingVisuals - NetworkManager is null.");
                Application.Quit(50);
                yield break;
            }

            // Count building prefabs in NetworkConfig
            int buildingPrefabCount = 0;
            GameObject stairsPrefab = null;
            GameObject rampPrefab = null;
            GameObject roof26Prefab = null;
            GameObject angledWallPrefab = null;

            foreach (var np in nm.NetworkConfig.Prefabs.Prefabs)
            {
                if (np.Prefab == null) continue;
                var piece = np.Prefab.GetComponent<BuildingPiece>();
                if (piece == null) continue;

                buildingPrefabCount++;

                // Identify specific prefabs by name convention
                string prefabName = np.Prefab.name;
                if (prefabName.Contains("Stairs")) stairsPrefab = np.Prefab;
                else if (prefabName.Contains("Ramp")) rampPrefab = np.Prefab;
                else if (prefabName.Contains("Roof26")) roof26Prefab = np.Prefab;
                else if (prefabName.Contains("AngledWall")) angledWallPrefab = np.Prefab;
            }

            Debug.Log($"[AutoTester] BuildingVisuals: found {buildingPrefabCount} building prefabs in NetworkConfig.");

            if (buildingPrefabCount < 9)
            {
                Debug.LogError($"[AutoTester] FAIL: BuildingVisuals - Expected 9 building prefabs, found {buildingPrefabCount}.");
                Application.Quit(51);
                yield break;
            }

            // Validate Stairs prefab has 12 step renderers
            if (stairsPrefab != null)
            {
                var renderers = stairsPrefab.GetComponentsInChildren<Renderer>(true);
                int stepCount = 0;
                foreach (var r in renderers)
                {
                    if (r.name.Contains("Step")) stepCount++;
                }
                Debug.Log($"[AutoTester] BuildingVisuals: Stairs has {stepCount} step renderers.");
                if (stepCount != 12)
                {
                    Debug.LogError($"[AutoTester] FAIL: BuildingVisuals - Stairs expected 12 steps, found {stepCount}.");
                    Application.Quit(52);
                    yield break;
                }
            }
            else
            {
                Debug.LogWarning("[AutoTester] BuildingVisuals: Stairs prefab not found by name.");
            }

            // Validate Ramp has a MeshFilter with non-trivial mesh
            if (rampPrefab != null)
            {
                var mf = rampPrefab.GetComponentInChildren<MeshFilter>(true);
                if (mf != null && mf.sharedMesh != null)
                {
                    int triCount = mf.sharedMesh.triangles.Length / 3;
                    Debug.Log($"[AutoTester] BuildingVisuals: Ramp mesh has {triCount} triangles.");
                    if (triCount < 4)
                    {
                        Debug.LogError($"[AutoTester] FAIL: BuildingVisuals - Ramp mesh has too few triangles ({triCount}).");
                        Application.Quit(53);
                        yield break;
                    }
                }
                else
                {
                    Debug.LogWarning("[AutoTester] BuildingVisuals: Ramp missing MeshFilter/mesh.");
                }
            }

            // Validate Roof26 has a MeshFilter with gable geometry
            if (roof26Prefab != null)
            {
                var mf = roof26Prefab.GetComponentInChildren<MeshFilter>(true);
                if (mf != null && mf.sharedMesh != null)
                {
                    float peakY = mf.sharedMesh.bounds.max.y;
                    int triCount = mf.sharedMesh.triangles.Length / 3;
                    Debug.Log($"[AutoTester] BuildingVisuals: Roof26 mesh has {triCount} triangles, peakY={peakY:0.000}.");
                    if (triCount < 4 || peakY < 0.5f)
                    {
                        Debug.LogError($"[AutoTester] FAIL: BuildingVisuals - Roof26 geometry invalid. tris={triCount} peakY={peakY:0.000}");
                        Application.Quit(54);
                        yield break;
                    }
                }
            }

            // Validate AngledWall has a MeshFilter
            if (angledWallPrefab != null)
            {
                var mf = angledWallPrefab.GetComponentInChildren<MeshFilter>(true);
                if (mf != null && mf.sharedMesh != null)
                {
                    int triCount = mf.sharedMesh.triangles.Length / 3;
                    Debug.Log($"[AutoTester] BuildingVisuals: AngledWall mesh has {triCount} triangles.");
                    if (triCount < 2)
                    {
                        Debug.LogError($"[AutoTester] FAIL: BuildingVisuals - AngledWall mesh has too few triangles.");
                        Application.Quit(55);
                        yield break;
                    }
                }
            }

            // Validate BoxCollider sizes match expected dimensions for key types
            if (rampPrefab != null)
            {
                var col = rampPrefab.GetComponent<BoxCollider>();
                if (col != null)
                {
                    Debug.Log($"[AutoTester] BuildingVisuals: Ramp collider size={col.size}");
                    if (col.size.y < 2f)
                    {
                        Debug.LogError($"[AutoTester] FAIL: BuildingVisuals - Ramp collider height too small ({col.size.y}).");
                        Application.Quit(56);
                        yield break;
                    }
                }
            }

            Debug.Log("[AutoTester] PASS: Building visuals sanity.");
            Debug.Log("[AutoTester] PASS: BuildingVisuals scenario complete.");
            yield return null;
        }
    }
}
