using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using ByteWar.Networking;
using ByteWar.Abilities;
using ByteWar.Survival;
using ByteWar.Building;
using ByteWar.Core;
using ByteWar.UI;
using System.Collections.Generic;
using System.IO;

namespace ByteWar.Editor
{
    public static class PrefabGenerator
    {
        private const string BuildGenPrefix = "[BuildGen]";

        [MenuItem("ByteWar/Generate Prefabs")]
        public static void GeneratePrefabs()
        {
            Debug.Log($"{BuildGenPrefix} PrefabGenerator: start");

            if (!DeployableAssetCatalogBuilder.TryRegenerate(out string catalogMessage))
                Debug.LogWarning($"{BuildGenPrefix} PrefabGenerator: deployable catalog regeneration failed before prefab generation. {catalogMessage}");
            else
                Debug.Log($"{BuildGenPrefix} PrefabGenerator: deployable catalog ready. {catalogMessage}");

            string basePath = "Assets/GeneratedPrefabs";
            if (!AssetDatabase.IsValidFolder(basePath))
            {
                AssetDatabase.CreateFolder("Assets", "GeneratedPrefabs");
                Debug.Log($"{BuildGenPrefix} PrefabGenerator: created folder: {basePath}");
            }

            // 1. Generate NetworkPlayer
            GameObject playerObj = new GameObject("NetworkPlayer");
            playerObj.AddComponent<NetworkObject>();
            playerObj.AddComponent<AttributeSet>();
            playerObj.AddComponent<AbilitySystemComponent>();
            playerObj.AddComponent<SurvivalStats>();
            playerObj.AddComponent<InventoryComponent>();
            playerObj.AddComponent<PlayerInputHandler>();
            playerObj.AddComponent<PlayerInteraction>();
            CharacterController cc = playerObj.AddComponent<CharacterController>();
            playerObj.AddComponent<PlayerMovement>();
            playerObj.AddComponent<PlayerVisualSetup>();
            playerObj.AddComponent<NetworkPlayer>();
            Animator animator = playerObj.AddComponent<Animator>();
            ClientNetworkAnimator netAnimator = playerObj.AddComponent<ClientNetworkAnimator>();
            netAnimator.Animator = animator;

            // Wire BuildingController onto the player
            var buildingController = playerObj.AddComponent<BuildingController>();

            // Comfort system for Valheim-style shelter/comfort mechanics
            playerObj.AddComponent<ComfortSystem>();

            // Audio + Footsteps
            playerObj.AddComponent<AudioSource>();
            playerObj.AddComponent<FootstepController>();

            // Generate building piece prefabs early so we can wire them into BuildingController before saving
            GameObject foundationPrefab = GenerateBuildingPiecePrefab(basePath, BuildingPieceType.Foundation, "Foundation", new Vector3(4f, 0.4f, 4f), new Color(0.55f, 0.45f, 0.35f));
            GameObject wallPrefab = GenerateBuildingPiecePrefab(basePath, BuildingPieceType.Wall, "Wall", new Vector3(4f, 3f, 0.3f), new Color(0.65f, 0.55f, 0.45f));
            GameObject floorPrefab = GenerateBuildingPiecePrefab(basePath, BuildingPieceType.Floor, "Floor", new Vector3(4f, 0.15f, 4f), new Color(0.6f, 0.5f, 0.4f));
            GameObject rampPrefab = GenerateBuildingPiecePrefab(basePath, BuildingPieceType.Ramp, "Ramp", new Vector3(4f, 3f, 4f), new Color(0.5f, 0.45f, 0.38f));
            float roofRidgeH = Mathf.Tan(26f * Mathf.Deg2Rad) * 2f;
            GameObject roof26Prefab = GenerateBuildingPiecePrefab(basePath, BuildingPieceType.Roof26, "Roof26", new Vector3(4f, roofRidgeH, 4f), new Color(0.45f, 0.25f, 0.2f));
            GameObject stairsPrefab = GenerateBuildingPiecePrefab(basePath, BuildingPieceType.Stairs, "Stairs", new Vector3(4f, 3f, 4f), new Color(0.55f, 0.5f, 0.42f));
            GameObject polePrefab = GenerateBuildingPiecePrefab(basePath, BuildingPieceType.Pole, "Pole", new Vector3(0.3f, 3f, 0.3f), new Color(0.5f, 0.4f, 0.3f));
            GameObject beamPrefab = GenerateBuildingPiecePrefab(basePath, BuildingPieceType.Beam, "Beam", new Vector3(4f, 0.3f, 0.3f), new Color(0.5f, 0.4f, 0.3f));
            float angledWallPeakH = Mathf.Tan(26f * Mathf.Deg2Rad) * 2f;
            GameObject angledWallPrefab = GenerateBuildingPiecePrefab(basePath, BuildingPieceType.AngledWall, "AngledWall", new Vector3(4f, angledWallPeakH, 0.3f), new Color(0.6f, 0.5f, 0.4f));
            // New Valheim-style pieces
            GameObject doorFramePrefab = GenerateBuildingPiecePrefab(basePath, BuildingPieceType.DoorFrame, "DoorFrame", new Vector3(4f, 3f, 0.3f), new Color(0.6f, 0.5f, 0.4f));
            GameObject windowPrefab = GenerateBuildingPiecePrefab(basePath, BuildingPieceType.Window, "Window", new Vector3(4f, 3f, 0.3f), new Color(0.62f, 0.55f, 0.45f));
            GameObject halfWallPrefab = GenerateBuildingPiecePrefab(basePath, BuildingPieceType.HalfWall, "HalfWall", new Vector3(4f, 1.5f, 0.3f), new Color(0.58f, 0.48f, 0.38f));
            buildingController.SetBuildingPrefabs(foundationPrefab, wallPrefab, floorPrefab, rampPrefab, roof26Prefab, stairsPrefab, polePrefab, beamPrefab, angledWallPrefab, doorFramePrefab, windowPrefab, halfWallPrefab);

            // Wire building recipes if they exist
            string[] recipeNames = { "FoundationRecipe", "WallRecipe", "FloorRecipe", "RampRecipe", "Roof26Recipe", "StairsRecipe", "PoleRecipe", "BeamRecipe", "AngledWallRecipe", "DoorFrameRecipe", "WindowRecipe", "HalfWallRecipe" };
            var recipes = new List<BuildingRecipe>();
            foreach (string rn in recipeNames)
            {
                var recipe = AssetDatabase.LoadAssetAtPath<BuildingRecipe>($"Assets/GeneratedAssets/{rn}.asset");
                if (recipe != null) recipes.Add(recipe);
            }
            if (recipes.Count > 0)
            {
                buildingController.SetRecipes(recipes);
                Debug.Log($"{BuildGenPrefix} PrefabGenerator: wired {recipes.Count} building recipes into BuildingController.");
            }
            else
            {
                Debug.LogWarning($"{BuildGenPrefix} PrefabGenerator: No building recipes found. Run Generate Assets first.");
            }

            Debug.Log($"{BuildGenPrefix} PrefabGenerator: created building piece prefabs.");

            // Deterministic controller config (prevents hovering from center.y = 0 defaults)
            cc.height = 2.0f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
            cc.skinWidth = 0.08f;
            cc.stepOffset = 0.4f;   // allow stepping onto stairs / small ledges
            cc.slopeLimit = 50f;    // allow walking up ramps (ramp angle ~36.8°)

            // Assign Animator Controller
            // Prefer the Resources copy so the controller is guaranteed to be present in builds.
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Resources/Generated/PlayerAnimatorController.controller");
            if (controller == null)
            {
                controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/GeneratedPrefabs/Animations/PlayerAnimatorController.controller");
            }
            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
            }
            else
            {
                Debug.LogError($"{BuildGenPrefix} PrefabGenerator: AnimatorController missing; NetworkPlayer will T-pose. Run Generate Animator Controller.");
            }

            // Add a simple visual representation
            GameObject mixamoPrefab = MixamoLocalAssets.AreAvailable(out _) ? FindMixamoCharacter() : null;
            GameObject humanoidPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedPrefabs/Models/HumanoidModel.prefab");
            // VisualRoot allows consistent mesh grounding across different imported model pivots.
            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(playerObj.transform);
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = Quaternion.identity;
            visualRoot.transform.localScale = Vector3.one;

            GameObject playerVisual;

            var networkPlayer = playerObj.GetComponent<NetworkPlayer>();

            if (mixamoPrefab != null)
            {
                playerVisual = (GameObject)PrefabUtility.InstantiatePrefab(mixamoPrefab);
                playerVisual.transform.SetParent(visualRoot.transform);
                playerVisual.transform.localPosition = Vector3.zero;
                PrefabUtility.UnpackPrefabInstance(playerVisual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                // Ensure the animator is on the root, not the child
                Animator childAnimator = playerVisual.GetComponent<Animator>();
                if (childAnimator != null)
                {
                    animator.avatar = childAnimator.avatar;
                    Object.DestroyImmediate(childAnimator);
                }
                Debug.Log($"{BuildGenPrefix} PrefabGenerator: attached Mixamo character='{mixamoPrefab.name}'");
            }
            else if (humanoidPrefab != null)
            {
                Debug.Log("[PrefabGenerator] Mixamo disabled/unavailable. Using HumanoidModel fallback.");
                playerVisual = (GameObject)PrefabUtility.InstantiatePrefab(humanoidPrefab);
                playerVisual.transform.SetParent(visualRoot.transform);
                playerVisual.transform.localPosition = Vector3.zero;
                // Unpack so visuals are embedded as plain GameObjects — avoids
                // stale nested-prefab GUID errors when the model prefab is regenerated.
                PrefabUtility.UnpackPrefabInstance(playerVisual,
                    PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            else
            {
                playerVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                playerVisual.name = "FallbackCapsule";
                playerVisual.transform.SetParent(visualRoot.transform);
                playerVisual.transform.localPosition = new Vector3(0, 1f, 0);
            }

            // Truthful visual mode stamp: detect what was actually built rather than trusting the code path.
            if (networkPlayer != null)
            {
                var visualSetup = playerObj.GetComponent<PlayerVisualSetup>();
                if (visualSetup != null)
                {
                    var detectedMode = visualSetup.DetectActualVisualMode(out _);
                    networkPlayer.SetGeneratedVisualModeStamp(detectedMode);
                    Debug.Log($"{BuildGenPrefix} PrefabGenerator: visual mode stamp={detectedMode}");
                }
            }

            // Ground visual so its lowest renderer point sits on y=0 relative to player root.
            GroundVisualToFeet(visualRoot.transform, playerObj.transform);

            // Add Camera Target for Cinemachine
            GameObject cameraTarget = new GameObject("CameraTarget");
            cameraTarget.transform.SetParent(playerObj.transform);
            cameraTarget.transform.localPosition = new Vector3(0, 1.25f, 0); // Default; runtime may adjust based on rig/bounds

            string networkPlayerPath = $"{basePath}/NetworkPlayer.prefab";
            GameObject playerPrefab = PrefabUtility.SaveAsPrefabAsset(playerObj, networkPlayerPath);
            Object.DestroyImmediate(playerObj);
            Debug.Log($"{BuildGenPrefix} PrefabGenerator: created NetworkPlayer prefabPath={networkPlayerPath}");

            // 2. Generate ResourceNode
            GameObject resourceObj = new GameObject("ResourceNode");
            resourceObj.AddComponent<NetworkObject>();
            resourceObj.AddComponent<ResourceNode>();

            // Add a simple visual representation
            GameObject resourceVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            resourceVisual.transform.SetParent(resourceObj.transform);
            resourceVisual.transform.localPosition = new Vector3(0, 0.5f, 0);

            // Add collider for raycasting
            BoxCollider resourceCollider = resourceObj.AddComponent<BoxCollider>();
            resourceCollider.center = new Vector3(0, 0.5f, 0);
            resourceCollider.size = new Vector3(1, 1, 1);

            GameObject resourcePrefab = PrefabUtility.SaveAsPrefabAsset(resourceObj, $"{basePath}/ResourceNode.prefab");
            Object.DestroyImmediate(resourceObj);
            Debug.Log($"{BuildGenPrefix} PrefabGenerator: created ResourceNode prefab.");

            // 3. Generate EnemyAI — uses procedural orc model
            CharacterGenerator.GenerateEnemyModel(); // saves OrcModel.prefab
            GameObject orcModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GeneratedPrefabs/Models/OrcModel.prefab");

            GameObject enemyObj = new GameObject("EnemyAI");
            enemyObj.AddComponent<NetworkObject>();
            enemyObj.AddComponent<AttributeSet>();
            enemyObj.AddComponent<EnemyAI>();

            // Attach orc visual
            if (orcModelPrefab != null)
            {
                GameObject orcVisual = (GameObject)PrefabUtility.InstantiatePrefab(orcModelPrefab);
                orcVisual.transform.SetParent(enemyObj.transform);
                orcVisual.transform.localPosition = new Vector3(0f, 0.47f, 0f); // ground the feet
                orcVisual.transform.localScale = Vector3.one * 0.90f;
                PrefabUtility.UnpackPrefabInstance(orcVisual,
                    PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Debug.Log($"{BuildGenPrefix} PrefabGenerator: Orc model attached to EnemyAI.");
            }
            else
            {
                // Fallback if model generation failed
                GameObject fb = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                fb.transform.SetParent(enemyObj.transform);
                fb.transform.localPosition = new Vector3(0f, 1f, 0f);
                var fbMat = new Material(Shader.Find("Standard")) { color = new Color(0.22f, 0.42f, 0.12f) };
                fb.GetComponent<Renderer>().sharedMaterial = fbMat;
                Object.DestroyImmediate(fb.GetComponent<Collider>());
                Debug.LogWarning($"{BuildGenPrefix} PrefabGenerator: Orc model not found, using fallback capsule.");
            }

            // CharacterController for physics-based movement (blocked by building colliders)
            CharacterController enemyCC = enemyObj.AddComponent<CharacterController>();
            enemyCC.center = new Vector3(0f, 1.1f, 0f);
            enemyCC.height = 2.2f;
            enemyCC.radius = 0.45f;
            enemyCC.skinWidth = 0.08f;
            enemyCC.stepOffset = 0.3f;

            GameObject enemyPrefab = PrefabUtility.SaveAsPrefabAsset(enemyObj, $"{basePath}/EnemyAI.prefab");
            Object.DestroyImmediate(enemyObj);
            Debug.Log($"{BuildGenPrefix} PrefabGenerator: created EnemyAI prefab with orc model.");

            // 4. Generate NetworkManager
            GameObject networkManagerObj = new GameObject("NetworkManager");
            NetworkManager networkManager = networkManagerObj.AddComponent<NetworkManager>();
            UnityTransport transport = networkManagerObj.AddComponent<UnityTransport>();
            networkManagerObj.AddComponent<ByteWar.Networking.CustomNetworkManagerHUD>();
            var bootstrapper = networkManagerObj.AddComponent<ByteWar.Networking.NetworkBootstrapper>();

            // Do NOT replace NetworkConfig with `new` — it breaks prefab serialization.
            // Modify the existing serialized instance instead.
            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.PlayerPrefab = playerPrefab;

            // Wire player prefab into bootstrapper so it can repair null refs at runtime
            var so = new SerializedObject(bootstrapper);
            so.FindProperty("_playerPrefab").objectReferenceValue = playerPrefab;
            so.FindProperty("_transport").objectReferenceValue = transport;
            so.ApplyModifiedProperties();

            // Register spawnable prefabs
            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = playerPrefab });
            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = resourcePrefab });
            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = enemyPrefab });
            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = foundationPrefab });
            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = wallPrefab });
            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = floorPrefab });
            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = rampPrefab });
            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = roof26Prefab });
            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = stairsPrefab });
            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = polePrefab });
            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = beamPrefab });
            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = angledWallPrefab });
            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = doorFramePrefab });
            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = windowPrefab });
            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = halfWallPrefab });

            // Optional generated deployable assets (Resources/Generated) that can be spawned by developer placement.
            TryAddOptionalGeneratedDeployablePrefab(networkManager, BlenderE2EPropPrefabGenerator.PrefabPath, "BlenderE2EProp");
            TryAddOptionalGeneratedDeployablePrefab(networkManager, LargeTreePrefabGenerator.PrefabPath, "LargeTree");
            TryAddCatalogGeneratedDeployablePrefabs(networkManager);

            // Wire WorldPersistence onto NetworkManager
            var worldPersistence = networkManagerObj.AddComponent<WorldPersistence>();
            worldPersistence.SetBuildingPrefabs(foundationPrefab, wallPrefab, floorPrefab, rampPrefab, roof26Prefab, stairsPrefab, polePrefab, beamPrefab, angledWallPrefab, doorFramePrefab, windowPrefab, halfWallPrefab);

            // Wire DevConsole onto NetworkManager (DontDestroyOnLoad)
            networkManagerObj.AddComponent<DevConsole>();
            Debug.Log($"{BuildGenPrefix} PrefabGenerator: DevConsole attached to NetworkManager prefab.");

            GameObject networkManagerPrefab = PrefabUtility.SaveAsPrefabAsset(networkManagerObj, $"{basePath}/NetworkManager.prefab");
            Object.DestroyImmediate(networkManagerObj);
            Debug.Log($"{BuildGenPrefix} PrefabGenerator: created NetworkManager prefab.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"{BuildGenPrefix} PrefabGenerator: prefab generation completed successfully.");
        }

        private static GameObject FindMixamoCharacter()
        {
            string folderPath = "Assets/Art/Characters/Mixamo";
            if (!AssetDatabase.IsValidFolder(folderPath)) return null;

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
                // Look for the base character model (not an animation)
                if (fileName.Contains("character") || fileName.Contains("bot") || fileName.Contains("paladin"))
                {
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }
            return null;
        }

        private static void GroundVisualToFeet(Transform visualRoot, Transform playerRoot)
        {
            var renderers = visualRoot.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                Debug.LogWarning("[PrefabGenerator] Visual grounding skipped: no renderers found.");
                return;
            }

            float minLocalY = float.PositiveInfinity;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                Vector3 localMin = playerRoot.InverseTransformPoint(r.bounds.min);
                if (localMin.y < minLocalY) minLocalY = localMin.y;
            }

            if (float.IsInfinity(minLocalY)) return;

            // If minLocalY is below 0, move visuals up; if above 0, move down.
            visualRoot.localPosition += new Vector3(0f, -minLocalY, 0f);
            Debug.Log($"[PrefabGenerator] Grounded VisualRoot by {-minLocalY:0.000} (minLocalY={minLocalY:0.000}).");
        }

        /// <summary>
        /// Generates a building piece prefab with type-specific visuals, a NetworkObject,
        /// BuildingPiece, and authored SnapPointMarker children for Valheim-style snapping.
        /// </summary>
        private static GameObject GenerateBuildingPiecePrefab(string basePath, BuildingPieceType type, string name, Vector3 size, Color color)
        {
            GameObject obj = new GameObject($"Building_{name}");
            obj.AddComponent<NetworkObject>();
            var piece = obj.AddComponent<BuildingPiece>();

            var tempPrefabPath = $"{basePath}/Building_{name}.prefab";

            // Ensure mesh folder exists
            string meshFolder = $"{basePath}/Meshes";
            if (!AssetDatabase.IsValidFolder(meshFolder))
                AssetDatabase.CreateFolder(basePath, "Meshes");

            // Get or create the shared material
            Material sharedMat = GetOrCreateMaterial(basePath, name, color);

            // Create type-specific visual
            CreateBuildingVisual(obj, type, name, size, sharedMat, meshFolder);

            // Collider on root — shape-accurate for walkable pieces
            AddCollider(obj, type, name, size, meshFolder);

            // Add authored snap point markers as children
            AddSnapPoints(obj, type);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, tempPrefabPath);
            Object.DestroyImmediate(obj);

            // Set private _pieceType via SerializedObject on the saved prefab
            var pieceSO = new SerializedObject(prefab.GetComponent<BuildingPiece>());
            pieceSO.FindProperty("_pieceType").enumValueIndex = (int)type;
            pieceSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(prefab);

            int snapCount = prefab.GetComponentsInChildren<SnapPointMarker>().Length;
            Debug.Log($"{BuildGenPrefix} PrefabGenerator: created Building_{name} prefab at {tempPrefabPath} with {snapCount} snap points");
            return prefab;
        }

        /// <summary>Gets or creates a material asset for a building piece.</summary>
        private static Material GetOrCreateMaterial(string basePath, string name, Color color)
        {
            string matPath = $"{basePath}/Building_{name}_Material.mat";
            Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existingMat != null)
            {
                existingMat.color = color;
                EditorUtility.SetDirty(existingMat);
                return existingMat;
            }

            var mat = new Material(Shader.Find("Standard")) { color = color };
            AssetDatabase.CreateAsset(mat, matPath);
            return AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }

        /// <summary>
        /// Dispatches visual creation by building piece type.
        /// Cube-based: Foundation, Wall, Floor, Pole, Beam, DoorFrame, Window, HalfWall.
        /// Custom mesh: Ramp, Roof26, AngledWall.
        /// Compound cubes: Stairs.
        /// </summary>
        private static void CreateBuildingVisual(GameObject root, BuildingPieceType type, string name,
            Vector3 size, Material mat, string meshFolder)
        {
            switch (type)
            {
                case BuildingPieceType.Stairs:
                    CreateStairsVisual(root, name, size, mat);
                    break;

                case BuildingPieceType.Ramp:
                    CreateCustomMeshVisual(root, name, size, mat, meshFolder,
                        BuildingMeshBuilder.BuildRampMesh(size.x, size.y, size.z));
                    break;

                case BuildingPieceType.Roof26:
                    {
                        float ridgeH = Mathf.Tan(26f * Mathf.Deg2Rad) * (size.x * 0.5f);
                        CreateCustomMeshVisual(root, name, size, mat, meshFolder,
                            BuildingMeshBuilder.BuildGableRoofMesh(size.x, size.z, ridgeH));
                        break;
                    }

                case BuildingPieceType.AngledWall:
                    {
                        float peakH = Mathf.Tan(26f * Mathf.Deg2Rad) * (size.x * 0.5f);
                        CreateCustomMeshVisual(root, name, size, mat, meshFolder,
                            BuildingMeshBuilder.BuildAngledWallMesh(size.x, peakH, size.z));
                        break;
                    }

                case BuildingPieceType.DoorFrame:
                    CreateDoorFrameVisual(root, name, size, mat);
                    break;

                case BuildingPieceType.Window:
                    CreateWindowVisual(root, name, size, mat);
                    break;

                default:
                    CreateCubeVisual(root, name, size, mat);
                    break;
            }
        }

        /// <summary>Creates a simple cube visual (Foundation, Wall, Floor, Pole, Beam).</summary>
        private static void CreateCubeVisual(GameObject root, string name, Vector3 size, Material mat)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = $"{name}Visual";
            visual.transform.SetParent(root.transform);
            visual.transform.localScale = size;
            visual.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            visual.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
        }

        /// <summary>Creates a 4-step staircase from stacked cubes.</summary>
        private static void CreateStairsVisual(GameObject root, string name, Vector3 size, Material mat)
        {
            const int stepCount = 12;
            float wallHeight = 3f;  // total rise of the staircase
            float stepH = wallHeight / stepCount;  // 0.25 per step — ankle-height for a 2-unit character
            float stepD = size.z / stepCount;
            float halfDepth = size.z * 0.5f;

            for (int i = 0; i < stepCount; i++)
            {
                float blockH = stepH * (i + 1);      // each step rises from ground to its top
                float centerY = blockH * 0.5f;
                float centerZ = -halfDepth + stepD * i + stepD * 0.5f;

                GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = $"{name}Step{i}";
                step.transform.SetParent(root.transform);
                step.transform.localScale = new Vector3(size.x, blockH, stepD);
                step.transform.localPosition = new Vector3(0f, centerY, centerZ);
                step.GetComponent<Renderer>().sharedMaterial = mat;
                Object.DestroyImmediate(step.GetComponent<Collider>());
            }
        }

        /// <summary>Creates a visual from a procedural mesh and saves it as an asset.</summary>
        private static void CreateCustomMeshVisual(GameObject root, string name, Vector3 size,
            Material mat, string meshFolder, Mesh mesh)
        {
            string meshPath = $"{meshFolder}/Building_{name}_Mesh.asset";
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existingMesh != null)
            {
                // Replace contents of existing mesh asset
                existingMesh.Clear();
                existingMesh.vertices = mesh.vertices;
                existingMesh.triangles = mesh.triangles;
                existingMesh.normals = mesh.normals;
                existingMesh.RecalculateBounds();
                EditorUtility.SetDirty(existingMesh);
                Object.DestroyImmediate(mesh);
                mesh = existingMesh;
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, meshPath);
                mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            }

            GameObject visual = new GameObject($"{name}Visual");
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            var mf = visual.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = visual.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
        }

        /// <summary>
        /// Adds a shape-accurate collider for each building piece type.
        /// Ramp/Roof26/AngledWall use MeshCollider (convex) so CharacterController can walk on them.
        /// Stairs use one BoxCollider per step so players can walk up individual steps.
        /// Everything else gets a single BoxCollider matching the bounding size.
        /// </summary>
        private static void AddCollider(GameObject obj, BuildingPieceType type, string name,
            Vector3 size, string meshFolder)
        {
            switch (type)
            {
                case BuildingPieceType.Stairs:
                    {
                        // One box collider per step — player can walk up each step
                        const int stepCount = 12;
                        float wallHeight = 3f;
                        float stepH = wallHeight / stepCount;  // 0.25 per step
                        float stepD = size.z / stepCount;
                        float halfDepth = size.z * 0.5f;

                        for (int i = 0; i < stepCount; i++)
                        {
                            var stepCol = new GameObject($"StepCol{i}");
                            stepCol.transform.SetParent(obj.transform);
                            float blockH = stepH * (i + 1);
                            float centerY = blockH * 0.5f;
                            float centerZ = -halfDepth + stepD * i + stepD * 0.5f;
                            stepCol.transform.localPosition = new Vector3(0f, centerY, centerZ);
                            stepCol.transform.localRotation = Quaternion.identity;
                            var bc = stepCol.AddComponent<BoxCollider>();
                            bc.center = Vector3.zero;
                            bc.size = new Vector3(size.x, blockH, stepD);
                        }
                        break;
                    }

                case BuildingPieceType.Ramp:
                    {
                        string meshPath = $"{meshFolder}/Building_{name}_Mesh.asset";
                        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                        if (mesh == null)
                        {
                            // Fallback: generate the mesh now (shouldn't happen since visual was created first)
                            mesh = BuildingMeshBuilder.BuildRampMesh(size.x, size.y, size.z);
                        }
                        var mc = obj.AddComponent<MeshCollider>();
                        mc.sharedMesh = mesh;
                        mc.convex = true;
                        break;
                    }

                case BuildingPieceType.Roof26:
                    {
                        string meshPath = $"{meshFolder}/Building_{name}_Mesh.asset";
                        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                        if (mesh == null)
                        {
                            float ridgeH = Mathf.Tan(26f * Mathf.Deg2Rad) * (size.x * 0.5f);
                            mesh = BuildingMeshBuilder.BuildGableRoofMesh(size.x, size.z, ridgeH);
                        }
                        var mc = obj.AddComponent<MeshCollider>();
                        mc.sharedMesh = mesh;
                        mc.convex = true;
                        break;
                    }

                case BuildingPieceType.AngledWall:
                    {
                        string meshPath = $"{meshFolder}/Building_{name}_Mesh.asset";
                        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                        if (mesh == null)
                        {
                            float peakH = Mathf.Tan(26f * Mathf.Deg2Rad) * (size.x * 0.5f);
                            mesh = BuildingMeshBuilder.BuildAngledWallMesh(size.x, peakH, size.z);
                        }
                        var mc = obj.AddComponent<MeshCollider>();
                        mc.sharedMesh = mesh;
                        mc.convex = true;
                        break;
                    }

                default:
                    {
                        BoxCollider col = obj.AddComponent<BoxCollider>();
                        col.center = new Vector3(0f, size.y * 0.5f, 0f);
                        col.size = size;
                        break;
                    }
            }
        }

        /// <summary>
        /// Creates SnapPointMarker child GameObjects at authored local positions with typed connections.
        /// Positions are chosen to enable Valheim-style edge/corner connections.
        /// Snap types enable smart matching: Top↔Bottom, Side↔Side, Corner↔Corner.
        /// </summary>
        private static void AddSnapPoints(GameObject root, BuildingPieceType type)
        {
            const float wallH = 3f;

            switch (type)
            {
                case BuildingPieceType.Foundation:
                    // 4 top corners (Corner type) + 4 edge midpoints (Side type)
                    CreateSnapChild(root, new Vector3(-2f, 0.4f, -2f), SnapPointType.Corner);
                    CreateSnapChild(root, new Vector3(-2f, 0.4f, 2f), SnapPointType.Corner);
                    CreateSnapChild(root, new Vector3(2f, 0.4f, -2f), SnapPointType.Corner);
                    CreateSnapChild(root, new Vector3(2f, 0.4f, 2f), SnapPointType.Corner);
                    CreateSnapChild(root, new Vector3(0f, 0.4f, -2f), SnapPointType.Side);
                    CreateSnapChild(root, new Vector3(0f, 0.4f, 2f), SnapPointType.Side);
                    CreateSnapChild(root, new Vector3(-2f, 0.4f, 0f), SnapPointType.Side);
                    CreateSnapChild(root, new Vector3(2f, 0.4f, 0f), SnapPointType.Side);
                    break;

                case BuildingPieceType.Wall:
                    // 4 corner snaps at wall surface (z=0 centerline, matching Valheim flat-plane walls)
                    CreateSnapChild(root, new Vector3(-2f, 0f, 0f), SnapPointType.Bottom);
                    CreateSnapChild(root, new Vector3(2f, 0f, 0f), SnapPointType.Bottom);
                    CreateSnapChild(root, new Vector3(-2f, wallH, 0f), SnapPointType.Top);
                    CreateSnapChild(root, new Vector3(2f, wallH, 0f), SnapPointType.Top);
                    break;

                case BuildingPieceType.Floor:
                    // 4 corners + 4 edge midpoints (all Bottom for stacking)
                    CreateSnapChild(root, new Vector3(-2f, 0f, -2f), SnapPointType.Corner);
                    CreateSnapChild(root, new Vector3(-2f, 0f, 2f), SnapPointType.Corner);
                    CreateSnapChild(root, new Vector3(2f, 0f, -2f), SnapPointType.Corner);
                    CreateSnapChild(root, new Vector3(2f, 0f, 2f), SnapPointType.Corner);
                    CreateSnapChild(root, new Vector3(0f, 0f, -2f), SnapPointType.Side);
                    CreateSnapChild(root, new Vector3(0f, 0f, 2f), SnapPointType.Side);
                    CreateSnapChild(root, new Vector3(-2f, 0f, 0f), SnapPointType.Side);
                    CreateSnapChild(root, new Vector3(2f, 0f, 0f), SnapPointType.Side);
                    break;

                case BuildingPieceType.Ramp:
                    // Bottom-front (Bottom) + top-back (Top) + side midpoints (Side)
                    CreateSnapChild(root, new Vector3(-2f, 0f, -2f), SnapPointType.Bottom);
                    CreateSnapChild(root, new Vector3(2f, 0f, -2f), SnapPointType.Bottom);
                    CreateSnapChild(root, new Vector3(-2f, wallH, 2f), SnapPointType.Top);
                    CreateSnapChild(root, new Vector3(2f, wallH, 2f), SnapPointType.Top);
                    CreateSnapChild(root, new Vector3(-2f, wallH * 0.5f, 0f), SnapPointType.Side);
                    CreateSnapChild(root, new Vector3(2f, wallH * 0.5f, 0f), SnapPointType.Side);
                    break;

                case BuildingPieceType.Roof26:
                    // 4 base corners + 4 base edge mids + 2 ridge points
                    float ridgeH = Mathf.Tan(26f * Mathf.Deg2Rad) * 2f;
                    CreateSnapChild(root, new Vector3(-2f, 0f, -2f), SnapPointType.Corner);
                    CreateSnapChild(root, new Vector3(-2f, 0f, 2f), SnapPointType.Corner);
                    CreateSnapChild(root, new Vector3(2f, 0f, -2f), SnapPointType.Corner);
                    CreateSnapChild(root, new Vector3(2f, 0f, 2f), SnapPointType.Corner);
                    CreateSnapChild(root, new Vector3(0f, 0f, -2f), SnapPointType.Side);
                    CreateSnapChild(root, new Vector3(0f, 0f, 2f), SnapPointType.Side);
                    CreateSnapChild(root, new Vector3(-2f, 0f, 0f), SnapPointType.Side);
                    CreateSnapChild(root, new Vector3(2f, 0f, 0f), SnapPointType.Side);
                    CreateSnapChild(root, new Vector3(0f, ridgeH, -2f), SnapPointType.Ridge);
                    CreateSnapChild(root, new Vector3(0f, ridgeH, 2f), SnapPointType.Ridge);
                    break;

                case BuildingPieceType.Stairs:
                    // Bottom corners + bottom mid + top corners + top mid
                    CreateSnapChild(root, new Vector3(-2f, 0f, -2f), SnapPointType.Bottom);
                    CreateSnapChild(root, new Vector3(2f, 0f, -2f), SnapPointType.Bottom);
                    CreateSnapChild(root, new Vector3(0f, 0f, -2f), SnapPointType.Bottom);
                    CreateSnapChild(root, new Vector3(-2f, wallH, 2f), SnapPointType.Top);
                    CreateSnapChild(root, new Vector3(2f, wallH, 2f), SnapPointType.Top);
                    CreateSnapChild(root, new Vector3(0f, wallH, 2f), SnapPointType.Top);
                    break;

                case BuildingPieceType.Pole:
                    // Bottom + top
                    CreateSnapChild(root, new Vector3(0f, 0f, 0f), SnapPointType.Bottom);
                    CreateSnapChild(root, new Vector3(0f, wallH, 0f), SnapPointType.Top);
                    break;

                case BuildingPieceType.Beam:
                    // Two endpoints (Side connections)
                    CreateSnapChild(root, new Vector3(-2f, 0f, 0f), SnapPointType.Side);
                    CreateSnapChild(root, new Vector3(2f, 0f, 0f), SnapPointType.Side);
                    break;

                case BuildingPieceType.AngledWall:
                    // Two base corners + one peak
                    CreateSnapChild(root, new Vector3(-2f, 0f, 0f), SnapPointType.Bottom);
                    CreateSnapChild(root, new Vector3(2f, 0f, 0f), SnapPointType.Bottom);
                    CreateSnapChild(root, new Vector3(0f, Mathf.Tan(26f * Mathf.Deg2Rad) * 2f, 0f), SnapPointType.Ridge);
                    break;

                case BuildingPieceType.DoorFrame:
                    // Same as Wall (4 centerline snaps) + 2 door-opening side snaps
                    CreateSnapChild(root, new Vector3(-2f, 0f, 0f), SnapPointType.Bottom);
                    CreateSnapChild(root, new Vector3(2f, 0f, 0f), SnapPointType.Bottom);
                    CreateSnapChild(root, new Vector3(-2f, wallH, 0f), SnapPointType.Top);
                    CreateSnapChild(root, new Vector3(2f, wallH, 0f), SnapPointType.Top);
                    // Door opening edge snaps
                    CreateSnapChild(root, new Vector3(-0.75f, 0f, 0f), SnapPointType.Side);
                    CreateSnapChild(root, new Vector3(0.75f, 0f, 0f), SnapPointType.Side);
                    break;

                case BuildingPieceType.Window:
                    // Same as Wall (4 centerline snaps)
                    CreateSnapChild(root, new Vector3(-2f, 0f, 0f), SnapPointType.Bottom);
                    CreateSnapChild(root, new Vector3(2f, 0f, 0f), SnapPointType.Bottom);
                    CreateSnapChild(root, new Vector3(-2f, wallH, 0f), SnapPointType.Top);
                    CreateSnapChild(root, new Vector3(2f, wallH, 0f), SnapPointType.Top);
                    break;

                case BuildingPieceType.HalfWall:
                    // Half-height wall (1.5m tall), 4 centerline snaps
                    CreateSnapChild(root, new Vector3(-2f, 0f, 0f), SnapPointType.Bottom);
                    CreateSnapChild(root, new Vector3(2f, 0f, 0f), SnapPointType.Bottom);
                    CreateSnapChild(root, new Vector3(-2f, 1.5f, 0f), SnapPointType.Top);
                    CreateSnapChild(root, new Vector3(2f, 1.5f, 0f), SnapPointType.Top);
                    break;
            }
        }

        /// <summary>Creates a child GameObject with a SnapPointMarker at the given local position and type.</summary>
        private static void CreateSnapChild(GameObject parent, Vector3 localPos, SnapPointType snapType = SnapPointType.Generic)
        {
            var child = new GameObject($"Snap_{localPos.x:0.##}_{localPos.y:0.##}_{localPos.z:0.##}");
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = localPos;
            child.transform.localRotation = Quaternion.identity;
            var marker = child.AddComponent<SnapPointMarker>();
            marker.SnapType = snapType;
        }

        /// <summary>Creates a door frame visual: wall with rectangular cutout in the center.</summary>
        private static void CreateDoorFrameVisual(GameObject root, string name, Vector3 size, Material mat)
        {
            float doorW = 1.5f;
            float doorH = 2.2f;
            float wallW = size.x;
            float wallH = size.y;
            float wallD = size.z;

            // Left pillar
            float pillarW = (wallW - doorW) * 0.5f;
            CreateCubePart(root, $"{name}_Left", new Vector3(pillarW, wallH, wallD),
                new Vector3(-wallW * 0.5f + pillarW * 0.5f, wallH * 0.5f, 0f), mat);

            // Right pillar
            CreateCubePart(root, $"{name}_Right", new Vector3(pillarW, wallH, wallD),
                new Vector3(wallW * 0.5f - pillarW * 0.5f, wallH * 0.5f, 0f), mat);

            // Lintel (top beam above door)
            float lintelH = wallH - doorH;
            CreateCubePart(root, $"{name}_Lintel", new Vector3(doorW, lintelH, wallD),
                new Vector3(0f, doorH + lintelH * 0.5f, 0f), mat);
        }

        /// <summary>Creates a window wall visual: wall with centered window cutout.</summary>
        private static void CreateWindowVisual(GameObject root, string name, Vector3 size, Material mat)
        {
            float winW = 1.5f;
            float winH = 1.2f;
            float winSill = 1.0f; // height from floor to window bottom
            float wallW = size.x;
            float wallH = size.y;
            float wallD = size.z;

            float pillarW = (wallW - winW) * 0.5f;

            // Left pillar (full height)
            CreateCubePart(root, $"{name}_Left", new Vector3(pillarW, wallH, wallD),
                new Vector3(-wallW * 0.5f + pillarW * 0.5f, wallH * 0.5f, 0f), mat);

            // Right pillar (full height)
            CreateCubePart(root, $"{name}_Right", new Vector3(pillarW, wallH, wallD),
                new Vector3(wallW * 0.5f - pillarW * 0.5f, wallH * 0.5f, 0f), mat);

            // Bottom section (below window)
            CreateCubePart(root, $"{name}_Sill", new Vector3(winW, winSill, wallD),
                new Vector3(0f, winSill * 0.5f, 0f), mat);

            // Top section (above window)
            float topH = wallH - winSill - winH;
            CreateCubePart(root, $"{name}_Top", new Vector3(winW, topH, wallD),
                new Vector3(0f, winSill + winH + topH * 0.5f, 0f), mat);
        }

        /// <summary>Creates a simple cube part as a child of the root (for compound piece visuals).</summary>
        private static void CreateCubePart(GameObject root, string partName, Vector3 scale, Vector3 localPos, Material mat)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.SetParent(root.transform);
            part.transform.localScale = scale;
            part.transform.localPosition = localPos;
            part.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(part.GetComponent<Collider>());
        }

        private static void TryAddOptionalGeneratedDeployablePrefab(NetworkManager networkManager, string prefabPath, string label)
        {
            if (networkManager == null || networkManager.NetworkConfig == null || networkManager.NetworkConfig.Prefabs == null)
                return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.Log($"{BuildGenPrefix} PrefabGenerator: optional deployable '{label}' missing at '{prefabPath}' (skipping registration).");
                return;
            }

            if (prefab.GetComponent<NetworkObject>() == null)
            {
                Debug.LogWarning($"{BuildGenPrefix} PrefabGenerator: optional deployable '{label}' has no NetworkObject at root; cannot register for NGO spawn.");
                return;
            }

            var prefabs = networkManager.NetworkConfig.Prefabs.Prefabs;
            for (int i = 0; i < prefabs.Count; i++)
            {
                var existing = prefabs[i];
                if (existing.Prefab == prefab)
                {
                    Debug.Log($"{BuildGenPrefix} PrefabGenerator: optional deployable '{label}' already registered.");
                    return;
                }
            }

            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
            Debug.Log($"{BuildGenPrefix} PrefabGenerator: registered optional deployable '{label}' in NetworkConfig.");
        }

        private static void TryAddCatalogGeneratedDeployablePrefabs(NetworkManager networkManager)
        {
            if (networkManager == null || networkManager.NetworkConfig == null || networkManager.NetworkConfig.Prefabs == null)
                return;

            var catalog = AssetDatabase.LoadAssetAtPath<DeployableAssetCatalog>(DeployableAssetCatalogBuilder.CatalogAssetPath);
            if (catalog == null || catalog.Entries == null || catalog.Entries.Count == 0)
            {
                Debug.Log($"{BuildGenPrefix} PrefabGenerator: deployable catalog missing or empty; skipping catalog-driven network prefab registration.");
                return;
            }

            int attempted = 0;
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                var entry = catalog.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ResourcePath))
                    continue;

                string resourcePath = NormalizeResourcePath(entry.ResourcePath);
                if (string.IsNullOrEmpty(resourcePath))
                    continue;

                string prefabPath = $"Assets/Resources/{resourcePath}.prefab";
                string label = string.IsNullOrWhiteSpace(entry.DisplayName) ? resourcePath : entry.DisplayName;
                TryAddOptionalGeneratedDeployablePrefab(networkManager, prefabPath, label);
                attempted++;
            }

            Debug.Log($"{BuildGenPrefix} PrefabGenerator: attempted catalog-driven deployable registration for {attempted} entry(s).");
        }

        private static string NormalizeResourcePath(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                return string.Empty;

            string normalized = resourcePath.Trim().Replace('\\', '/');
            while (normalized.Contains("//", System.StringComparison.Ordinal))
                normalized = normalized.Replace("//", "/", System.StringComparison.Ordinal);

            int slash = normalized.LastIndexOf('/');
            int dot = normalized.LastIndexOf('.');
            if (dot > slash)
                normalized = normalized.Substring(0, dot);

            return normalized.Trim('/');
        }
    }
}
