using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace SurvivalRPG.Editor
{
    public static class SceneGenerator
    {
        [MenuItem("SurvivalRPG/Generate Test Scene")]
        public static void GenerateTestScene()
        {
            Debug.Log("[SceneGenerator] Starting test scene generation...");

            // Ensure Scenes folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            string scenePath = "Assets/Scenes/TestScene.unity";
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 1. Terrain ────────────────────────────────────────────────────────
            TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(
                "Assets/GeneratedPrefabs/GeneratedTerrainData.asset");
            if (terrainData != null)
            {
                GameObject terrainObj = Terrain.CreateTerrainGameObject(terrainData);
                terrainObj.name = "GeneratedTerrain";
                terrainObj.transform.position = new Vector3(-250f, 0f, -250f);
                var terrain = terrainObj.GetComponent<Terrain>();
                terrain.drawTreesAndFoliage = true;
                terrain.heightmapMaximumLOD = 0;
                terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
                Debug.Log("[SceneGenerator] Terrain added.");
            }
            else
            {
                // Fallback: large coloured plane
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Floor";
                floor.transform.position = Vector3.zero;
                floor.transform.localScale = new Vector3(50f, 1f, 50f);
                floor.GetComponent<Renderer>().sharedMaterial.color = new Color(0.22f, 0.50f, 0.18f);
                Debug.LogWarning("[SceneGenerator] TerrainData not found — using fallback plane.");
            }

            // ── 2. Procedural Skybox & Render Settings ────────────────────────────
            SetupSkyboxAndAmbient();

            // ── 3. Sun (directional light) ────────────────────────────────────────
            GameObject sunObj = new GameObject("Sun");
            Light sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.00f, 0.95f, 0.82f);  // warm golden
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.80f;
            sun.shadowBias = 0.02f;
            sun.shadowNormalBias = 0.04f;
            sunObj.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Debug.Log("[SceneGenerator] Sun light configured.");

            // ── 4. Soft fill light (blue sky bounce) ─────────────────────────────
            GameObject fillObj = new GameObject("FillLight");
            Light fill = fillObj.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.55f, 0.65f, 0.90f);  // cool blue sky
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;
            fillObj.transform.rotation = Quaternion.Euler(30f, 148f, 0f);
            Debug.Log("[SceneGenerator] Fill light configured.");

            // ── 5. Main Camera with WoW-style ThirdPersonCamera ───────────────────
            GameObject cameraObj = new GameObject("Main Camera");
            Camera camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.53f, 0.63f, 0.82f);
            camera.farClipPlane = 800f;
            cameraObj.tag = "MainCamera";
            // Start camera behind/above origin; ThirdPersonCamera will reposition it once
            // the local player spawns and calls SetTarget().
            cameraObj.transform.position = new Vector3(0f, 8f, -10f);
            cameraObj.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
            cameraObj.AddComponent<AudioListener>();
            cameraObj.AddComponent<SurvivalRPG.Core.ThirdPersonCamera>();
            Debug.Log("[SceneGenerator] ThirdPersonCamera added to Main Camera.");

            // ── 6. NetworkManager ─────────────────────────────────────────────────
            GameObject nmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GeneratedPrefabs/NetworkManager.prefab");
            if (nmPrefab != null)
            {
                PrefabUtility.InstantiatePrefab(nmPrefab);
                Debug.Log("[SceneGenerator] NetworkManager added.");
            }
            else
            {
                Debug.LogWarning("[SceneGenerator] NetworkManager prefab not found — run Generate Prefabs first.");
            }

            // ── 7. Resource Nodes ─────────────────────────────────────────────────
            GameObject resourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GeneratedPrefabs/ResourceNode.prefab");
            if (resourcePrefab != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * 60f * Mathf.Deg2Rad;
                    float radius = 18f;
                    var r = (GameObject)PrefabUtility.InstantiatePrefab(resourcePrefab);
                    r.transform.position = new Vector3(
                        Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                }
                Debug.Log("[SceneGenerator] Resource nodes placed.");
            }

            // ── 8. Enemy Spawner ──────────────────────────────────────────────────
            GameObject spawnerObj = new GameObject("EnemySpawner");
            spawnerObj.transform.position = new Vector3(30f, 0f, 30f);
            spawnerObj.AddComponent<Unity.Netcode.NetworkObject>();
            var spawner = spawnerObj.AddComponent<SurvivalRPG.Survival.EnemySpawner>();
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GeneratedPrefabs/EnemyAI.prefab");
            if (enemyPrefab != null)
            {
                var so = new SerializedObject(spawner);
                so.FindProperty("_enemyPrefab").objectReferenceValue = enemyPrefab;
                so.ApplyModifiedProperties();
                Debug.Log("[SceneGenerator] EnemySpawner configured.");
            }

            // ── 9. UI ─────────────────────────────────────────────────────────────
            GameObject uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GeneratedPrefabs/UI/PlayerHUD.prefab");
            if (uiPrefab != null)
            {
                PrefabUtility.InstantiatePrefab(uiPrefab);
                Debug.Log("[SceneGenerator] HUD added.");
            }

            // ── 10. EventSystem (New Input System) ───────────────────────────────
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Debug.Log("[SceneGenerator] EventSystem with InputSystemUIInputModule added.");
            // ── 10b. Screen Logger for build-time debugging ─────────────────────
            GameObject loggerObj = new GameObject("ScreenLogger");
            loggerObj.AddComponent<SurvivalRPG.Core.ScreenLogger>();
            Debug.Log("[SceneGenerator] ScreenLogger added (toggle with F1).");
            // ── 11. Scatter environment objects ───────────────────────────────────
            EnvironmentGenerator.GenerateEnvironment();

            // ── Save ──────────────────────────────────────────────────────────────
            EditorSceneManager.SaveScene(newScene, scenePath);
            Debug.Log($"[SceneGenerator] Scene saved to {scenePath}");
        }

        // ──────────────────────────────────────────────────────────────────────────

        private static void SetupSkyboxAndAmbient()
        {
            // Procedural skybox (warm mid-day)
            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                string skyPath = "Assets/GeneratedPrefabs/ProceduralSkybox.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(skyPath) != null)
                    AssetDatabase.DeleteAsset(skyPath);

                var skyMat = new Material(skyShader);
                skyMat.SetFloat("_SunSize", 0.04f);
                skyMat.SetFloat("_SunSizeConvergence", 5f);
                skyMat.SetFloat("_AtmosphereThickness", 1.1f);
                skyMat.SetColor("_SkyTint", new Color(0.53f, 0.63f, 0.82f));
                skyMat.SetColor("_GroundColor", new Color(0.35f, 0.40f, 0.30f));
                skyMat.SetFloat("_Exposure", 1.2f);
                AssetDatabase.CreateAsset(skyMat, skyPath);
                RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
                Debug.Log("[SceneGenerator] Procedural skybox applied.");
            }

            // Trilight ambient
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.50f, 0.60f, 0.78f);  // blue sky
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.46f, 0.32f);  // green ground bounce
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.12f, 0.08f);  // dark earth

            // Distance fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.62f, 0.70f, 0.82f);
            RenderSettings.fogDensity = 0.0025f;

            Debug.Log("[SceneGenerator] Sky, ambient and fog configured.");
        }
    }
}
