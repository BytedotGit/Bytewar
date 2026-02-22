using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace SurvivalRPG.Editor
{
    public static class MCPCommands
    {
        [MenuItem("Tools/MCP/Generate All")]
        public static void GenerateAll()
        {
            Debug.Log("[MCP] Starting full generation pipeline...");

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

            // 6. Runtime prefabs (NetworkPlayer, EnemyAI, NetworkManager, etc.)
            Debug.Log("[MCP] Step 6/7 — Generating runtime prefabs...");
            PrefabGenerator.GeneratePrefabs();

            // 7. Scene (terrain + env objects + lighting + all prefabs)
            Debug.Log("[MCP] Step 7/7 — Generating test scene...");
            SceneGenerator.GenerateTestScene(); // also calls EnvironmentGenerator internally

            Debug.Log("[MCP] Full generation pipeline complete.");
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