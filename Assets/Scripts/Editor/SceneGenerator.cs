using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using ByteWar.UI;

namespace ByteWar.Editor
{
    public static class SceneGenerator
    {
        [MenuItem("ByteWar/Generate Test Scene")]
        public static void GenerateTestScene()
        {
            Debug.Log("[SceneGenerator] Starting test scene generation...");

            // Ensure Scenes folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            string scenePath = "Assets/Scenes/TestScene.unity";
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 1. Greybox Ground ─────────────────────────────────────────────────
            GenerateGreyboxGround();

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
            cameraObj.AddComponent<ByteWar.Core.ThirdPersonCamera>();
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
            var spawner = spawnerObj.AddComponent<ByteWar.Survival.EnemySpawner>();
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
            loggerObj.AddComponent<ByteWar.Core.ScreenLogger>();
            Debug.Log("[SceneGenerator] ScreenLogger added (toggle with F1).");
            // ── 10c. Keybinding HUD overlay ──────────────────────────────────────
            GameObject keybindingHudObj = new GameObject("KeybindingHUD");
            keybindingHudObj.AddComponent<ByteWar.UI.KeybindingHUD>();
            Debug.Log("[SceneGenerator] KeybindingHUD added (toggle with F2).");

            // ── 10d. Asset Deploy UI (for viewing/spawning test assets) ─────────
            // Visible only while holding Shift+Tab.
            GameObject assetDeployUiObj = new GameObject("AssetDeployUI");
            assetDeployUiObj.AddComponent<AssetDeployUI>();
            Debug.Log("[SceneGenerator] AssetDeployUI added (hold Shift+Tab).");

            // ── 11. Scatter greybox environment props ────────────────────────────
            GenerateGreyboxProps();

            // ── 12. Place Blender E2E prop near spawn ───────────────────────────
            TryPlaceBlenderE2EPropNearSpawn();

            // ── Save ──────────────────────────────────────────────────────────────
            EditorSceneManager.SaveScene(newScene, scenePath);
            Debug.Log($"[SceneGenerator] Scene saved to {scenePath}");
        }

        private static void TryPlaceBlenderE2EPropNearSpawn()
        {
            const string prefabPath = "Assets/Resources/Generated/BlenderE2EProp/BlenderE2EProp.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[SceneGenerator] Blender E2E prop prefab not found at {prefabPath}. Run Generate Blender E2E Prop Prefab first.");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = "Placed_BlenderE2EProp";

            Vector3 desired = new Vector3(4f, 2f, 4f);
            float y = 0f;
            if (Physics.Raycast(desired + Vector3.up * 50f, Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                y = hit.point.y;

            go.transform.position = new Vector3(desired.x, y + 0.1f, desired.z);
            go.transform.rotation = Quaternion.Euler(0f, 25f, 0f);

            Debug.Log($"[SceneGenerator] Placed BlenderE2EProp near spawn at {go.transform.position}.");
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

        // ── Greybox Ground ────────────────────────────────────────────────────────

        private static Material GetOrCreateGreyboxMaterial(string name, Color color, string basePath)
        {
            string matPath = $"{basePath}/Greybox_{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null)
            {
                existing.color = color;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var mat = new Material(Shader.Find("Standard")) { color = color };
            AssetDatabase.CreateAsset(mat, matPath);
            return AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }

        private static void GenerateGreyboxGround()
        {
            const string basePath = "Assets/GeneratedPrefabs";

            // Materials
            Material groundMat = GetOrCreateGreyboxMaterial("Ground", new Color(0.45f, 0.45f, 0.45f), basePath);
            Material accentMat = GetOrCreateGreyboxMaterial("Accent", new Color(0.35f, 0.35f, 0.38f), basePath);

            // Main ground: a large flat cube centered at origin. Thickness = 1m, top surface at Y=0.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "GreyboxGround";
            ground.isStatic = true;
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(200f, 1f, 200f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;
            ground.layer = LayerMask.NameToLayer("Default");

            // Raised platform near center for variety
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "GreyboxPlatform";
            platform.isStatic = true;
            platform.transform.position = new Vector3(30f, 0.5f, 20f);
            platform.transform.localScale = new Vector3(12f, 1f, 12f);
            platform.GetComponent<Renderer>().sharedMaterial = accentMat;

            // Ramp connecting platform to ground
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "GreyboxRamp";
            ramp.isStatic = true;
            ramp.transform.position = new Vector3(24f, 0.25f, 20f);
            ramp.transform.localScale = new Vector3(8f, 0.1f, 6f);
            ramp.transform.rotation = Quaternion.Euler(0f, 0f, -7f);
            ramp.GetComponent<Renderer>().sharedMaterial = accentMat;

            // A few walls / obstacles for navigation testing
            CreateGreyboxWall("GreyboxWallA", new Vector3(-15f, 1.5f, 10f), new Vector3(0.5f, 3f, 8f), accentMat);
            CreateGreyboxWall("GreyboxWallB", new Vector3(10f, 1.5f, -20f), new Vector3(12f, 3f, 0.5f), accentMat);

            Debug.Log("[SceneGenerator] Greybox ground generated (200x200m, Y=0 surface).");
        }

        private static void CreateGreyboxWall(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.isStatic = true;
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void GenerateGreyboxProps()
        {
            const string basePath = "Assets/GeneratedPrefabs";
            Material propMat = GetOrCreateGreyboxMaterial("Props", new Color(0.40f, 0.42f, 0.40f), basePath);

            // Scatter a few simple cubes as "rocks" around the map
            var rng = new System.Random(42);
            for (int i = 0; i < 20; i++)
            {
                float x = (float)(rng.NextDouble() * 160 - 80);
                float z = (float)(rng.NextDouble() * 160 - 80);
                float scale = 0.8f + (float)rng.NextDouble() * 1.5f;

                GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prop.name = $"GreyboxRock_{i}";
                prop.isStatic = true;
                prop.transform.position = new Vector3(x, scale * 0.5f, z);
                prop.transform.localScale = new Vector3(scale, scale * 0.7f, scale * 0.9f);
                prop.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360), 0f);
                prop.GetComponent<Renderer>().sharedMaterial = propMat;
            }

            Debug.Log("[SceneGenerator] Greybox props scattered (20 rocks).");
        }

        private static void PlaceBlenderE2EPropNearSpawn()
        {
            const string prefabPath = "Assets/Resources/Generated/BlenderE2EProp/BlenderE2EProp.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[SceneGenerator] BlenderE2EProp prefab not found at '{prefabPath}'. Run Generate Blender E2E Prop Prefab first.");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = "Placed_BlenderE2EProp";

            Vector3 pos = new Vector3(4f, 0f, 2f);
            if (Physics.Raycast(new Vector3(pos.x, 10f, pos.z), Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore))
                pos.y = hit.point.y;

            go.transform.position = pos;
            go.transform.rotation = Quaternion.identity;
            Debug.Log($"[SceneGenerator] Placed BlenderE2EProp at {pos}.");
        }
    }
}
