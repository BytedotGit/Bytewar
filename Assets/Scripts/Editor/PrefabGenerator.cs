using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using SurvivalRPG.Networking;
using SurvivalRPG.Abilities;
using SurvivalRPG.Survival;
using SurvivalRPG.Core;
using System.Collections.Generic;
using System.IO;

namespace SurvivalRPG.Editor
{
    public static class PrefabGenerator
    {
        private const string BuildGenPrefix = "[BuildGen]";

        [MenuItem("SurvivalRPG/Generate Prefabs")]
        public static void GeneratePrefabs()
        {
            Debug.Log($"{BuildGenPrefix} PrefabGenerator: start");

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
            playerObj.AddComponent<NetworkPlayer>();
            Animator animator = playerObj.AddComponent<Animator>();
            ClientNetworkAnimator netAnimator = playerObj.AddComponent<ClientNetworkAnimator>();
            netAnimator.Animator = animator;

            // Deterministic controller config (prevents hovering from center.y = 0 defaults)
            cc.height = 2.0f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
            cc.skinWidth = 0.08f;

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
                if (networkPlayer != null) networkPlayer.SetGeneratedVisualModeStamp(PlayerVisualMode.Mixamo);
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
                if (networkPlayer != null) networkPlayer.SetGeneratedVisualModeStamp(PlayerVisualMode.HumanoidFallback);
            }
            else
            {
                playerVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                playerVisual.name = "FallbackCapsule";
                playerVisual.transform.SetParent(visualRoot.transform);
                playerVisual.transform.localPosition = new Vector3(0, 1f, 0);
                if (networkPlayer != null) networkPlayer.SetGeneratedVisualModeStamp(PlayerVisualMode.PrimitiveFallback);
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

            // Collider
            CapsuleCollider enemyCollider = enemyObj.AddComponent<CapsuleCollider>();
            enemyCollider.center = new Vector3(0f, 1.1f, 0f);
            enemyCollider.height = 2.4f;
            enemyCollider.radius = 0.45f;

            GameObject enemyPrefab = PrefabUtility.SaveAsPrefabAsset(enemyObj, $"{basePath}/EnemyAI.prefab");
            Object.DestroyImmediate(enemyObj);
            Debug.Log($"{BuildGenPrefix} PrefabGenerator: created EnemyAI prefab with orc model.");

            // 4. Generate NetworkManager
            GameObject networkManagerObj = new GameObject("NetworkManager");
            NetworkManager networkManager = networkManagerObj.AddComponent<NetworkManager>();
            UnityTransport transport = networkManagerObj.AddComponent<UnityTransport>();
            networkManagerObj.AddComponent<SurvivalRPG.Networking.CustomNetworkManagerHUD>();
            var bootstrapper = networkManagerObj.AddComponent<SurvivalRPG.Networking.NetworkBootstrapper>();

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
    }
}
