using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace ByteWar.Editor
{
    public static class MCPCommands
    {
        [MenuItem("Tools/MCP/Reimport Quaternius FBX")]
        public static void ReimportQuaterniusFbx()
        {
            string[] roots =
            {
                "Assets/Art/Premade/Quaternius/UltimateNature/FBX",
                "Assets/Art/Premade/Quaternius/UltimateCrops/FBX",
                "Assets/Art/Premade/Quaternius/Environment/Vegetation",
            };
            int count = 0;
            foreach (string root in roots)
            {
                if (!AssetDatabase.IsValidFolder(root))
                    continue;
                string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { root });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                        count++;
                    }
                }
            }
            Debug.Log($"[MCP] Force-reimported {count} Quaternius FBX assets.");
        }

        [MenuItem("Tools/MCP/Generate All")]
        public static void GenerateAll()
        {
            Debug.Log("[MCP] Starting full generation pipeline...");

            // 0. Reimport Quaternius FBX to apply latest postprocessor settings
            Debug.Log("[MCP] Step 0 — Reimporting Quaternius FBX assets...");
            ReimportQuaterniusFbx();

            // 1. Terrain assets (heightmap, layers, textures, tree prefab)
            Debug.Log("[MCP] Step 1/7 — Generating terrain assets...");
            TerrainGenerator.GenerateTerrain();

            // Pre-step: delete stale prefabs that reference old model GUIDs to prevent
            // import errors during the intermediate asset-refresh that happens when
            // CharacterGenerator deletes/recreates the model prefabs.
            string[] stalePrefabs = {
                "Assets/GeneratedPrefabs/NetworkPlayer.prefab",
                "Assets/GeneratedPrefabs/EnemyAI.prefab",
                "Assets/GeneratedPrefabs/NetworkManager.prefab"
            };
            foreach (var p in stalePrefabs)
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p) != null)
                    AssetDatabase.DeleteAsset(p);
            AssetDatabase.Refresh();

            // 2. Process Mixamo FBX assets into Humanoid rig (must run before AnimatorGenerator)
            Debug.Log("[MCP] Step 2/7 — Processing Mixamo assets...");
            MixamoProcessor.ProcessAssets();

            // 2b. Generate procedural humanoid as fallback model
            CharacterGenerator.GenerateHumanoidModel();

            // 3. Animator controller
            Debug.Log("[MCP] Step 3/7 — Generating animator...");
            AnimatorGenerator.GenerateAnimatorController();

            // 4. UI prefabs
            Debug.Log("[MCP] Step 4/7 — Generating UI prefabs...");
            UIGenerator.GenerateUIPrefab();

            // 5. ScriptableObject assets
            Debug.Log("[MCP] Step 5/7 — Generating scriptable assets...");
            AssetGenerator.GenerateAssets();

            // 6. Blender-generated deployable prefabs (optional, safe no-op if FBX missing)
            Debug.Log("[MCP] Step 6/7 — Generating Blender deployable prefabs...");
            BlenderE2EPropPrefabGenerator.Generate();
            LargeTreePrefabGenerator.Generate();
            LargeTreeVariationPrefabGenerator.GenerateAll();

            // Keep deployable catalog synchronized before NetworkManager prefab registration.
            if (DeployableAssetCatalogBuilder.TryRegenerate(out string catalogMessage))
                Debug.Log($"[MCP] Deployable catalog regeneration succeeded. {catalogMessage}");
            else
                Debug.LogWarning($"[MCP] Deployable catalog regeneration failed. {catalogMessage}");

            // 6b. Runtime prefabs (NetworkPlayer, EnemyAI, NetworkManager, etc.)
            Debug.Log("[MCP] Step 6b/7 — Generating runtime prefabs...");
            PrefabGenerator.GeneratePrefabs();

            // 7. Scene (terrain + env objects + lighting + all prefabs)
            Debug.Log("[MCP] Step 7/7 — Generating test scene...");
            SceneGenerator.GenerateTestScene(); // also calls EnvironmentGenerator internally

            Debug.Log("[MCP] Full generation pipeline complete.");
        }

        [MenuItem("Tools/MCP/Generate Blender E2E Prop Prefab")]
        public static void GenerateBlenderE2EProp()
        {
            Debug.Log("[MCP] Generating Blender E2E prop prefab...");
            bool ok = BlenderE2EPropPrefabGenerator.Generate();
            Debug.Log("[MCP] Blender E2E prop prefab generation complete.");

            if (Application.isBatchMode)
            {
                int exitCode = ok ? 0 : 1;
                Debug.Log($"[MCP] Exiting editor with code {exitCode} (batchmode). ok={ok}");
                EditorApplication.Exit(exitCode);
            }
        }

        [MenuItem("Tools/MCP/Generate Large Tree Prefab")]
        public static void GenerateLargeTreePrefab()
        {
            Debug.Log("[MCP] Generating LargeTree prefab...");
            bool ok = LargeTreePrefabGenerator.Generate();
            Debug.Log("[MCP] LargeTree prefab generation complete.");

            if (Application.isBatchMode)
            {
                int exitCode = ok ? 0 : 1;
                Debug.Log($"[MCP] Exiting editor with code {exitCode} (batchmode). ok={ok}");
                EditorApplication.Exit(exitCode);
            }
        }

        [MenuItem("Tools/MCP/Generate Large Tree Variation Prefabs")]
        public static void GenerateLargeTreeVariationPrefabs()
        {
            Debug.Log("[MCP] Generating LargeTree variation prefabs...");
            bool ok = LargeTreeVariationPrefabGenerator.GenerateAll();
            Debug.Log($"[MCP] LargeTree variation prefab generation complete. success={ok}");

            if (Application.isBatchMode)
            {
                int exitCode = ok ? 0 : 1;
                Debug.Log($"[MCP] Exiting editor with code {exitCode} (batchmode). ok={ok}");
                EditorApplication.Exit(exitCode);
            }
        }

        public static void GenerateTerrain()
        {
            Debug.Log("[MCP] Generating terrain...");
            TerrainGenerator.GenerateTerrain();
            Debug.Log("[MCP] Terrain generation complete.");
        }

        public static void BuildProject()
        {
            Debug.Log("[MCP] Building project...");
            BuildScript.BuildWindowsClient();
            Debug.Log("[MCP] Build complete.");
        }

        public static void RunTests()
        {
            Debug.Log("[MCP] Running tests...");
            // This would require a more complex setup to run tests and return results
            // For now, we just log it.
            Debug.Log("[MCP] Tests run complete.");
        }

        public static string GetSceneHierarchy()
        {
            var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            var hierarchy = new List<string>();
            foreach (var obj in rootObjects)
            {
                hierarchy.Add(GetGameObjectHierarchy(obj, 0));
            }
            return string.Join("\n", hierarchy);
        }

        private static string GetGameObjectHierarchy(GameObject obj, int depth)
        {
            string indent = new string('-', depth * 2);
            string result = $"{indent} {obj.name} ({string.Join(", ", obj.GetComponents<Component>().Select(c => c.GetType().Name))})";
            foreach (Transform child in obj.transform)
            {
                result += "\n" + GetGameObjectHierarchy(child.gameObject, depth + 1);
            }
            return result;
        }
    }
}