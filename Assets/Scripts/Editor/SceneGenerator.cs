using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using ByteWar.UI;
using System;

namespace ByteWar.Editor
{
    public static class SceneGenerator
    {
        private static readonly string[] LargeTreeGrowthStages =
        {
            "Seedling",
            "Sapling",
            "Young",
            "Mature",
            "Adult",
        };

        private const int BiomeTreePlacementCount = 32;
        private const string GeneratedTerrainDataPath = "Assets/GeneratedPrefabs/GeneratedTerrainData.asset";

        [MenuItem("ByteWar/Generate Test Scene")]
        public static void GenerateTestScene()
        {
            Debug.Log("[SceneGenerator] Starting test scene generation...");

            // Ensure Scenes folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            string scenePath = "Assets/Scenes/TestScene.unity";
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 1. Terrain-first world generation (fallback to greybox if needed) ─
            bool terrainPlaced = TryGenerateAndPlaceTerrain();
            if (!terrainPlaced)
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
                    float x = Mathf.Cos(angle) * radius;
                    float z = Mathf.Sin(angle) * radius;
                    r.transform.position = ResolveGroundPosition(x, z);
                }
                Debug.Log("[SceneGenerator] Resource nodes placed.");
            }

            // ── 8. Enemy Spawner ──────────────────────────────────────────────────
            GameObject spawnerObj = new GameObject("EnemySpawner");
            spawnerObj.transform.position = ResolveGroundPosition(30f, 30f, 0.25f);
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

            // ── 10d. Asset Deploy UI (tap-B browser + placement flow) ───────────
            // Open browser with B, click category/item to pick nested entries.
            GameObject assetDeployUiObj = new GameObject("AssetDeployUI");
            assetDeployUiObj.AddComponent<AssetDeployUI>();
            Debug.Log("[SceneGenerator] AssetDeployUI added (tap B browser).");

            // ── 11. Scatter environment props based on active world surface ──────
            if (terrainPlaced)
            {
                EnvironmentGenerator.GenerateEnvironment();
            }
            else
            {
                GenerateGreyboxProps();
            }

            // ── 12. Place Blender E2E prop near spawn ───────────────────────────
            TryPlaceBlenderE2EPropNearSpawn();

            // ── 13. Legacy LargeTree showcase/ring placements removed ──────────
            // Quaternius scatter now owns default world vegetation composition.

            // ── Save ──────────────────────────────────────────────────────────────
            EditorSceneManager.SaveScene(newScene, scenePath);
            Debug.Log($"[SceneGenerator] Scene saved to {scenePath}");
        }

        private static bool TryGenerateAndPlaceTerrain()
        {
            try
            {
                TerrainGenerator.GenerateTerrain();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneGenerator] TerrainGenerator threw an exception. Falling back to emergency terrain generation. {ex}");
            }

            TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(GeneratedTerrainDataPath);
            if (terrainData == null)
            {
                Debug.LogWarning($"[SceneGenerator] TerrainData missing at '{GeneratedTerrainDataPath}'. Attempting emergency terrain generation.");
                terrainData = TryCreateEmergencyTerrainDataAsset();
                if (terrainData == null)
                {
                    Debug.LogWarning("[SceneGenerator] Emergency terrain generation failed. Falling back to greybox ground.");
                    return false;
                }
            }

            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = "GeneratedTerrain";
            terrainObject.isStatic = true;

            // Center terrain around world origin so spawn/content placements remain symmetric.
            terrainObject.transform.position = new Vector3(-terrainData.size.x * 0.5f, 0f, -terrainData.size.z * 0.5f);

            Debug.Log($"[SceneGenerator] Procedural terrain placed at {terrainObject.transform.position} with size {terrainData.size}.");
            return true;
        }

        private static TerrainData TryCreateEmergencyTerrainDataAsset()
        {
            try
            {
                if (!AssetDatabase.IsValidFolder("Assets/GeneratedPrefabs"))
                    AssetDatabase.CreateFolder("Assets", "GeneratedPrefabs");

                const int resolution = 257;
                var terrainData = new TerrainData
                {
                    heightmapResolution = resolution,
                    size = new Vector3(500f, 45f, 500f)
                };

                // Emergency deterministic terrain so scene generation never collapses to a barren flat greybox.
                float[,] heights = new float[resolution, resolution];
                for (int z = 0; z < resolution; z++)
                {
                    float nz = z / (float)(resolution - 1);
                    for (int x = 0; x < resolution; x++)
                    {
                        float nx = x / (float)(resolution - 1);
                        float broad = Mathf.PerlinNoise(nx * 2.9f + 11.3f, nz * 2.9f + 7.1f) * 0.09f;
                        float detail = Mathf.PerlinNoise(nx * 8.2f + 31.7f, nz * 8.2f + 19.4f) * 0.025f;
                        float h = broad + detail;

                        float dx = nx - 0.5f;
                        float dz = nz - 0.5f;
                        float dist = Mathf.Sqrt((dx * dx) + (dz * dz));
                        float flatten = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.10f, 0.24f, dist));
                        heights[z, x] = Mathf.Lerp(0.045f, h, flatten);
                    }
                }

                terrainData.SetHeights(0, 0, heights);

                if (AssetDatabase.LoadAssetAtPath<TerrainData>(GeneratedTerrainDataPath) != null)
                    AssetDatabase.DeleteAsset(GeneratedTerrainDataPath);

                AssetDatabase.CreateAsset(terrainData, GeneratedTerrainDataPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                TerrainData saved = AssetDatabase.LoadAssetAtPath<TerrainData>(GeneratedTerrainDataPath);
                if (saved != null)
                    Debug.Log($"[SceneGenerator] Emergency terrain data saved to '{GeneratedTerrainDataPath}'.");

                return saved;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneGenerator] Emergency terrain generation failed. {ex}");
                return null;
            }
        }

        private static Vector3 ResolveGroundPosition(float x, float z, float yOffset = 0f)
        {
            return new Vector3(x, ResolveGroundHeight(x, z) + yOffset, z);
        }

        private static float ResolveGroundHeight(float x, float z)
        {
            if (Terrain.activeTerrain != null)
            {
                Terrain terrain = Terrain.activeTerrain;
                return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
            }

            Vector3 probe = new Vector3(x, 400f, z);
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;

            return 0f;
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

            Vector3 desired = new Vector3(4f, 0f, 4f);
            float y = ResolveGroundHeight(desired.x, desired.z);
            go.transform.position = new Vector3(desired.x, y, desired.z);
            go.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
            AlignObjectBottomToGround(go, y, 0.02f);

            Debug.Log($"[SceneGenerator] Placed BlenderE2EProp near spawn at {go.transform.position}.");
        }

        private static void TryPlaceLargeTreeNearSpawn()
        {
            const string prefabPath = "Assets/Resources/Generated/LargeTree/LargeTree.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[SceneGenerator] LargeTree prefab not found at {prefabPath}. Run Generate Large Tree Prefab first.");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = "Placed_LargeTree";

            Vector3 desired = new Vector3(10f, 0f, 8f);
            float y = ResolveGroundHeight(desired.x, desired.z);

            go.transform.position = new Vector3(desired.x, y, desired.z);
            go.transform.rotation = Quaternion.Euler(0f, 10f, 0f);
            AlignObjectBottomToGround(go, y, 0.02f);

            TryAutoUprightTreeInstance(go, go.name);

            Debug.Log($"[SceneGenerator] Placed LargeTree near spawn at {go.transform.position}.");
        }

        private static void TryPlaceLargeTreeGrowthShowcaseNearSpawn()
        {
            const string variantsRoot = "Assets/Resources/Generated/LargeTree/Variants";
            Vector3 showcaseCenter = new Vector3(10f, 0f, 8f);
            float spacing = 3.2f;
            int placedCount = 0;

            for (int i = 0; i < LargeTreeGrowthStages.Length; i++)
            {
                string stageName = LargeTreeGrowthStages[i];
                string prefabPath = $"{variantsRoot}/{stageName}/LargeTree_{stageName}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[SceneGenerator] Growth-stage prefab missing at {prefabPath}.");
                    continue;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = $"Placed_LargeTree_{stageName}";

                float xOffset = (i - ((LargeTreeGrowthStages.Length - 1) * 0.5f)) * spacing;
                float zOffset = Mathf.Abs(i - 2) * 0.6f;
                Vector3 desired = showcaseCenter + new Vector3(xOffset, 0f, zOffset);

                float y = ResolveGroundHeight(desired.x, desired.z);

                go.transform.position = new Vector3(desired.x, y, desired.z);
                go.transform.rotation = Quaternion.Euler(0f, 12f + (i * 16f), 0f);
                AlignObjectBottomToGround(go, y, 0.02f);

                // Variation prefabs already have upright correction baked in during
                // prefab generation — skip the scene-level upright correction to
                // avoid double-rotating prefabs with sculpted mesh extents.

                placedCount++;
                Debug.Log($"[SceneGenerator] Placed growth-stage LargeTree '{stageName}' at {go.transform.position}.");
            }

            if (placedCount == 0)
            {
                Debug.LogWarning("[SceneGenerator] No growth-stage LargeTree variants were found; placing base LargeTree fallback.");
                TryPlaceLargeTreeNearSpawn();
                return;
            }

            Debug.Log($"[SceneGenerator] Growth-stage LargeTree showcase placed ({placedCount}/{LargeTreeGrowthStages.Length}).");
        }

        private static void TryPlaceLargeTreeBiomeRingNearSpawn()
        {
            const string variantsRoot = "Assets/Resources/Generated/LargeTree/Variants";
            const float innerRadius = 18f;
            const float outerRadius = 48f;

            int placedCount = 0;
            for (int i = 0; i < BiomeTreePlacementCount; i++)
            {
                int stageIndex = (i * 3 + 1) % LargeTreeGrowthStages.Length;
                string stageName = LargeTreeGrowthStages[stageIndex];
                string prefabPath = $"{variantsRoot}/{stageName}/LargeTree_{stageName}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                    continue;

                float angleDegrees = (i * 137.50776f) % 360f;
                float radialT = ((i * 0.61803395f) + 0.25f) % 1f;
                float radius = Mathf.Lerp(innerRadius, outerRadius, radialT);
                float angleRadians = angleDegrees * Mathf.Deg2Rad;

                Vector3 desired = new Vector3(
                    Mathf.Cos(angleRadians) * radius,
                    0f,
                    Mathf.Sin(angleRadians) * radius);

                float y = ResolveGroundHeight(desired.x, desired.z);

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = $"Placed_BiomeTree_{stageName}_{i:00}";
                go.transform.position = new Vector3(desired.x, y, desired.z);
                go.transform.rotation = Quaternion.Euler(0f, (angleDegrees + 12f) % 360f, 0f);
                AlignObjectBottomToGround(go, y, 0.02f);

                // Variation prefabs already have upright correction baked in during
                // prefab generation — skip to avoid double-rotating.
                placedCount++;
            }

            Debug.Log($"[SceneGenerator] Biome tree ring placed ({placedCount}/{BiomeTreePlacementCount}).");
        }

        private static void TryAutoUprightTreeInstance(GameObject treeRoot, string contextName)
        {
            if (treeRoot == null)
                return;

            Vector3 renderableSize = ComputeRenderableBoundsSize(treeRoot);
            Quaternion correction = ResolveSceneTreeUprightRotation(renderableSize);
            if (correction == Quaternion.identity)
                return;

            treeRoot.transform.rotation = correction * treeRoot.transform.rotation;
            Debug.Log($"[SceneGenerator] Upright correction applied to '{contextName}' ({correction.eulerAngles}, size={renderableSize}).");
        }

        public static Quaternion ResolveSceneTreeUprightRotation(Vector3 renderableSize)
        {
            float sx = Mathf.Abs(renderableSize.x);
            float sy = Mathf.Abs(renderableSize.y);
            float sz = Mathf.Abs(renderableSize.z);

            float horizontalMax = Mathf.Max(sx, sz);
            if (horizontalMax <= 0.001f)
                return Quaternion.identity;

            if (sy >= horizontalMax * 0.55f)
                return Quaternion.identity;

            if (sz >= sx)
                return Quaternion.Euler(-90f, 0f, 0f);

            return Quaternion.Euler(0f, 0f, 90f);
        }

        private static Vector3 ComputeRenderableBoundsSize(GameObject modelRoot)
        {
            if (modelRoot == null)
                return Vector3.zero;

            bool hasBounds = false;
            Bounds aggregate = default;
            var renderers = modelRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    aggregate = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                aggregate.Encapsulate(renderer.bounds);
            }

            return hasBounds ? aggregate.size : Vector3.zero;
        }

        private static void AlignObjectBottomToGround(GameObject target, float groundY, float clearance)
        {
            if (target == null)
                return;

            if (TryComputeRenderableBounds(target, out Bounds bounds))
            {
                float offset = (groundY + clearance) - bounds.min.y;
                target.transform.position += Vector3.up * offset;
                return;
            }

            var rootCollider = target.GetComponent<Collider>();
            if (rootCollider != null)
            {
                float offset = (groundY + clearance) - rootCollider.bounds.min.y;
                target.transform.position += Vector3.up * offset;
            }
        }

        private static bool TryComputeRenderableBounds(GameObject modelRoot, out Bounds bounds)
        {
            bounds = default;
            if (modelRoot == null)
                return false;

            bool hasBounds = false;
            var renderers = modelRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.bounds.size.sqrMagnitude <= 0.000001f)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds;
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
