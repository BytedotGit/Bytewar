using System.Collections;
using System;
using ByteWar.Building;
using ByteWar.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ByteWar.Core
{
    /// <summary>
    /// Core AutoTester scenario: validates player spawn, visuals, camera, grounding,
    /// animation, building system presence, and movement. This is the baseline scenario
    /// that must always pass.
    /// </summary>
    public class AutoTestScenarioCore : IAutoTestScenario
    {
        private const int MaxLegacyEnvironmentProps = 0;
        private const int MinQuaterniusDecorTrees = 200;
        private const int MinQuaterniusDecorVegetation = 300;
        private const int MinQuaterniusDecorRocks = 180;
        private const int MinQuaterniusShowcaseTrees = 1;
        private const int MinQuaterniusShowcaseVegetation = 1;
        private const int MinQuaterniusShowcaseRocks = 1;

        public string Name => "Core";

        public IEnumerator Run(AutoTesterContext ctx)
        {
            var localPlayer = ctx.LocalPlayer;
            var inputHandler = ctx.InputHandler;
            var mainCam = ctx.MainCamera;
            var tpc = ctx.ThirdPersonCamera;
            var animator = ctx.Animator;

            if (!TryValidateWorldPopulation(out string worldSummary, out string worldFailure))
            {
                Debug.LogError($"[AutoTester] FAIL: Core - World population sanity failed. reason='{worldFailure}' summary='{worldSummary}'");
                Application.Quit(43);
                yield break;
            }
            Debug.Log($"[AutoTester] PASS: World population sanity. {worldSummary}");

            // --- Player visual sanity ---
            var stamp = localPlayer.GeneratedVisualModeStamp;
            var actual = localPlayer.DetectActualVisualMode(out NetworkPlayer.VisualDiagnostics diag);
            Debug.Log($"[AutoTester] Visual sanity: stamp={stamp} actual={actual} child='{diag.VisualRootChildName}' skinned={diag.SkinnedMeshRendererCount} renderers={diag.RendererCount} mixamoRig={diag.HasMixamoRig}");

            if (stamp == PlayerVisualMode.Unknown)
            {
                Debug.LogError("[AutoTester] FAIL: Core - Player visual mode stamp is Unknown.");
                Application.Quit(28);
                yield break;
            }

            if (stamp != actual)
            {
                Debug.LogError($"[AutoTester] FAIL: Core - Player visuals mismatch. stamp={stamp} actual={actual}.");
                Application.Quit(29);
                yield break;
            }
            Debug.Log("[AutoTester] PASS: Player visual sanity.");

            // --- Animation sanity ---
            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogError("[AutoTester] FAIL: Core - Animator.runtimeAnimatorController is null.");
                Application.Quit(31);
                yield break;
            }
            if (animator.avatar == null)
            {
                Debug.LogError("[AutoTester] FAIL: Core - Animator.avatar is null.");
                Application.Quit(32);
                yield break;
            }

            Transform leftHand = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            if (leftHand == null)
                Debug.LogWarning("[AutoTester] Animation sanity: LeftHand bone not found (non-humanoid?).");

            // --- Camera sanity ---
            if (tpc.Target == null)
            {
                Debug.LogError("[AutoTester] FAIL: Core - ThirdPersonCamera.Target is null.");
                Application.Quit(25);
                yield break;
            }

            float pivotDelta = tpc.PivotWorldPosition.y - localPlayer.transform.position.y;
            Debug.Log($"[AutoTester] Camera sanity: target='{tpc.Target.name}' pivotHeight={tpc.PivotHeight:0.00} pivotDeltaY={pivotDelta:0.00}");

            if (pivotDelta < 0.8f || pivotDelta > 2.0f)
            {
                Debug.LogError($"[AutoTester] FAIL: Core - Camera pivot delta out of range. pivotDeltaY={pivotDelta:0.00}");
                Application.Quit(26);
                yield break;
            }
            Debug.Log("[AutoTester] PASS: Camera pivot sanity.");

            // --- First-person zoom sanity ---
            Debug.Log("[AutoTester] Forcing first-person zoom sanity...");
            tpc.AutoTest_SetTargetDistance(0f, immediate: true);
            tpc.AutoTest_TickCameraTransformOnce();
            yield return null;
            tpc.AutoTest_TickCameraTransformOnce();

            Vector3 fpPivot = tpc.PivotWorldPosition;
            Vector3 camPos = mainCam.transform.position;
            Quaternion camRot = mainCam.transform.rotation;
            float fpDist = Vector3.Distance(camPos, fpPivot);
            Debug.Log($"[AutoTester] First-person sanity: camPos={camPos} pivot={fpPivot} dist={fpDist:0.000}");

            if (!IsFinite(camPos) || !IsFinite(camRot))
            {
                Debug.LogError("[AutoTester] FAIL: Core - First-person zoom camera contains NaN/Inf.");
                Application.Quit(35);
                yield break;
            }
            if (fpDist > 0.05f)
            {
                Debug.LogError($"[AutoTester] FAIL: Core - First-person camera not at pivot. dist={fpDist:0.000}");
                Application.Quit(36);
                yield break;
            }

            Transform visualRoot = localPlayer.transform.Find("VisualRoot");
            if (visualRoot != null)
            {
                var renderers = visualRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
                if (renderers != null && renderers.Length > 0)
                {
                    bool anyEnabled = false;
                    foreach (var r in renderers)
                    {
                        if (r != null && r.enabled) { anyEnabled = true; break; }
                    }
                    Debug.Log($"[AutoTester] First-person sanity: visualRootRenderers={renderers.Length} anyEnabled={anyEnabled}");
                    if (anyEnabled)
                    {
                        Debug.LogError("[AutoTester] FAIL: Core - First-person renderers still enabled.");
                        Application.Quit(37);
                        yield break;
                    }
                }
            }
            Debug.Log("[AutoTester] PASS: First-person zoom sanity.");

            // --- Grounding sanity ---
            var cc = ctx.CharacterController;
            if (cc != null)
            {
                yield return new WaitForSeconds(0.5f);
                float groundY;

                Vector3 groundProbe = localPlayer.transform.position + Vector3.up * 2f;
                if (Physics.Raycast(groundProbe, Vector3.down, out RaycastHit groundHit, 20f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    groundY = groundHit.point.y;
                    Debug.Log($"[AutoTester] Grounding probe hit '{groundHit.collider.name}' at y={groundY:0.000}");

                    float capsuleBottom = localPlayer.transform.position.y + cc.center.y - (cc.height * 0.5f);
                    float delta = capsuleBottom - groundY;
                    Debug.Log($"[AutoTester] Grounding sanity: capsuleBottom={capsuleBottom:0.000} groundY={groundY:0.000} delta={delta:0.000}");

                    if (Mathf.Abs(delta) > 0.45f)
                    {
                        Debug.LogError($"[AutoTester] FAIL: Core - Player capsule not grounded. delta={delta:0.000}");
                        Application.Quit(27);
                        yield break;
                    }
                    Debug.Log("[AutoTester] PASS: Grounding sanity.");
                }
                else if (Terrain.activeTerrain != null)
                {
                    groundY = Terrain.activeTerrain.SampleHeight(localPlayer.transform.position)
                            + Terrain.activeTerrain.transform.position.y;

                    float capsuleBottom = localPlayer.transform.position.y + cc.center.y - (cc.height * 0.5f);
                    float delta = capsuleBottom - groundY;
                    Debug.Log($"[AutoTester] Grounding sanity: capsuleBottom={capsuleBottom:0.000} groundY={groundY:0.000} delta={delta:0.000}");

                    if (Mathf.Abs(delta) > 0.45f)
                    {
                        Debug.LogError($"[AutoTester] FAIL: Core - Player capsule not grounded. delta={delta:0.000}");
                        Application.Quit(27);
                        yield break;
                    }
                    Debug.Log("[AutoTester] PASS: Grounding sanity.");
                }
                else
                {
                    Debug.LogWarning("[AutoTester] Grounding sanity skipped (no ground).");
                }
            }

            // --- Visual grounding sanity ---
            if (localPlayer != null)
            {
                Transform vr = localPlayer.transform.Find("VisualRoot");
                if (vr != null && VisualGroundingUtility.TryGetSupportGroundY(localPlayer.transform.position, ignoreRoot: localPlayer.transform, animator, out float vgGroundY, out string groundSource))
                {
                    if (VisualGroundingUtility.TrySampleVisualBottomY(vr, animator, out var sample))
                    {
                        float delta = sample.SelectedBottomY - vgGroundY;
                        Debug.Log($"[AutoTester] Visual grounding sanity: groundY={vgGroundY:0.000} source={groundSource} visualY={sample.SelectedBottomY:0.000} method={sample.Method} delta={delta:0.000}");

                        // Tolerance of 1.2 to accommodate Mixamo humanoid animation bone variance:
                        // idle/walking cycles swing toe positions ~0.8–1.0 units during the stride.
                        // A delta beyond ±1.2 indicates the visual root has catastrophically sunk
                        // underground (e.g. missing grounding offset or root motion conflict).
                        if (Mathf.Abs(delta) > 1.2f)
                        {
                            Debug.LogError($"[AutoTester] FAIL: Core - Visual grounding failed. delta={delta:0.000}");
                            Application.Quit(38);
                            yield break;
                        }
                        Debug.Log("[AutoTester] PASS: Visual grounding sanity.");
                    }
                }
            }

            // --- Building system sanity ---
            var buildingController = localPlayer.GetComponent<BuildingController>();
            if (buildingController == null)
            {
                Debug.LogError("[AutoTester] FAIL: Core - BuildingController missing.");
                Application.Quit(39);
                yield break;
            }

            int recipeCount = buildingController.Recipes != null ? buildingController.Recipes.Count : 0;
            Debug.Log($"[AutoTester] Building sanity: recipes={recipeCount}");

            WorldPersistence worldPersistence = null;
            if (Unity.Netcode.NetworkManager.Singleton != null)
                worldPersistence = Unity.Netcode.NetworkManager.Singleton.GetComponent<WorldPersistence>();

            if (worldPersistence != null)
                Debug.Log($"[AutoTester] Building sanity: WorldPersistence present. savePath='{worldPersistence.SaveFilePath}'");

            Debug.Log("[AutoTester] PASS: Building system sanity.");

            // --- Movement verification ---
            inputHandler.EnsureActionsEnabled("AutoTester preflight");
            if (Keyboard.current == null)
            {
                Debug.LogError("[AutoTester] FAIL: Core - Keyboard.current is null.");
                Application.Quit(22);
                yield break;
            }

            Vector3 startPos = localPlayer.transform.position;
            Quaternion handRotStart = leftHand != null ? leftHand.rotation : Quaternion.identity;
            float yawStart = localPlayer.transform.eulerAngles.y;
            float camYawStart = tpc.CameraYaw;

            // Ensure deterministic movement semantics for this check regardless of host mouse state.
            tpc.AutoTest_SetMouseState(leftHeld: false, rightHeld: false, leftDragging: false);

            Debug.Log($"[AutoTester] StartPos={startPos}");
            Debug.Log("[AutoTester] Simulating movement input (Forward) for 1.5s...");
            inputHandler.SetSimulatedMovement(new Vector2(0, 1));
            yield return new WaitForSeconds(1.5f);

            Vector3 afterForwardPos = localPlayer.transform.position;

            Debug.Log("[AutoTester] Simulating movement input (Right) for 1.5s (expected: turn in place when RMB is not held)...");
            inputHandler.SetSimulatedMovement(new Vector2(1, 0));
            yield return new WaitForSeconds(1.5f);

            inputHandler.SetSimulatedMovement(Vector2.zero);
            inputHandler.ClearSimulatedMovement();

            if (leftHand != null)
            {
                float handAngle = Quaternion.Angle(handRotStart, leftHand.rotation);
                Debug.Log($"[AutoTester] Animation sanity: LeftHand angle delta={handAngle:0.00} deg");

                if (handAngle < 1.0f)
                {
                    Debug.LogError("[AutoTester] FAIL: Core - Animation sanity failed (hand bone did not move).");
                    Application.Quit(33);
                    yield break;
                }
                Debug.Log("[AutoTester] PASS: Animation sanity.");
            }

            Vector3 endPos = localPlayer.transform.position;
            float moved = Vector3.Distance(startPos, endPos);
            Debug.Log($"[AutoTester] EndPos={endPos} moved={moved:0.000}");

            float yawEnd = localPlayer.transform.eulerAngles.y;
            float yawDelta = Mathf.Abs(Mathf.DeltaAngle(yawStart, yawEnd));
            Debug.Log($"[AutoTester] Turn sanity: yawStart={yawStart:0.0} yawEnd={yawEnd:0.0} yawDelta={yawDelta:0.0}");

            float camYawEnd = tpc.CameraYaw;
            float camYawDelta = Mathf.Abs(Mathf.DeltaAngle(camYawStart, camYawEnd));
            Debug.Log($"[AutoTester] Camera follow sanity: camYawStart={camYawStart:0.0} camYawEnd={camYawEnd:0.0} camYawDelta={camYawDelta:0.0}");

            Vector3 afterTurnPos = localPlayer.transform.position;
            float movedDuringTurn = Vector2.Distance(
                new Vector2(afterForwardPos.x, afterForwardPos.z),
                new Vector2(afterTurnPos.x, afterTurnPos.z));
            Debug.Log($"[AutoTester] Turn sanity: movedDuringTurn={movedDuringTurn:0.000}");

            if (yawDelta < 5f)
            {
                Debug.LogError("[AutoTester] FAIL: Core - Expected A/D input to turn player when RMB is not held.");
                Application.Quit(40);
                yield break;
            }

            if (camYawDelta < 5f)
            {
                Debug.LogError("[AutoTester] FAIL: Core - Expected camera to follow A/D turning when RMB is not held.");
                Application.Quit(42);
                yield break;
            }

            // Turning in place should not translate meaningfully.
            if (movedDuringTurn > 0.35f)
            {
                Debug.LogError($"[AutoTester] FAIL: Core - Player translated too much during turn-only input. movedDuringTurn={movedDuringTurn:0.000}");
                Application.Quit(41);
                yield break;
            }

            if (moved > 0.25f)
            {
                Debug.Log("[AutoTester] PASS: Movement changed player position.");
            }
            else
            {
                Debug.LogError("[AutoTester] FAIL: Core - Player did not move.");
                Application.Quit(10);
                yield break;
            }

            Debug.Log("[AutoTester] PASS: Core scenario complete.");
        }

        internal static bool TryValidateWorldPopulation(out string summary, out string failureReason)
        {
            int rockClusters = 0;
            int boulders = 0;
            int fallenLogs = 0;
            int biomeTrees = 0;
            int showcaseTrees = 0;
            int quaterniusDecorTrees = 0;
            int quaterniusDecorVegetation = 0;
            int quaterniusDecorRocks = 0;
            int quaterniusShowcaseTrees = 0;
            int quaterniusShowcaseVegetation = 0;
            int quaterniusShowcaseRocks = 0;
            bool hasGreybox = false;

            var transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null)
                    continue;

                string name = t.name;
                if (name.StartsWith("RockCluster", StringComparison.Ordinal))
                    rockClusters++;
                else if (name.StartsWith("Boulder", StringComparison.Ordinal))
                    boulders++;
                else if (name.StartsWith("FallenLog", StringComparison.Ordinal))
                    fallenLogs++;
                else if (name.StartsWith("Placed_BiomeTree_", StringComparison.Ordinal))
                    biomeTrees++;
                else if (name.StartsWith("Placed_LargeTree_", StringComparison.Ordinal))
                    showcaseTrees++;
                else if (name.StartsWith("Placed_QuaterniusTree_", StringComparison.Ordinal))
                {
                    quaterniusDecorTrees++;
                }
                else if (name.StartsWith("Placed_QuaterniusVegetation_", StringComparison.Ordinal))
                {
                    quaterniusDecorVegetation++;
                }
                else if (name.StartsWith("Placed_QuaterniusRock_", StringComparison.Ordinal))
                {
                    quaterniusDecorRocks++;
                }
                else if (name.StartsWith("Showcase_QuaterniusTree_", StringComparison.Ordinal))
                {
                    quaterniusShowcaseTrees++;
                }
                else if (name.StartsWith("Showcase_QuaterniusVegetation_", StringComparison.Ordinal))
                {
                    quaterniusShowcaseVegetation++;
                }
                else if (name.StartsWith("Showcase_QuaterniusRock_", StringComparison.Ordinal))
                {
                    quaterniusShowcaseRocks++;
                }

                if (!hasGreybox && name.StartsWith("Greybox", StringComparison.Ordinal))
                    hasGreybox = true;
            }

            int legacyEnvironmentProps = rockClusters + boulders + fallenLogs;
            bool hasTerrain = Terrain.activeTerrain != null;

            summary = $"terrain={hasTerrain} legacyProps={legacyEnvironmentProps} (rocks={rockClusters}, boulders={boulders}, logs={fallenLogs}) biomeTrees={biomeTrees} showcaseTrees={showcaseTrees} quaterniusTrees={quaterniusDecorTrees} quaterniusVegetation={quaterniusDecorVegetation} quaterniusRocks={quaterniusDecorRocks} quaterniusShowcaseTrees={quaterniusShowcaseTrees} quaterniusShowcaseVegetation={quaterniusShowcaseVegetation} quaterniusShowcaseRocks={quaterniusShowcaseRocks} greybox={hasGreybox}";

            if (!hasTerrain)
            {
                failureReason = "No active terrain present.";
                return false;
            }

            if (hasGreybox)
            {
                failureReason = "Greybox fallback objects detected in scene.";
                return false;
            }

            if (legacyEnvironmentProps > MaxLegacyEnvironmentProps)
            {
                failureReason = $"Legacy placeholder props detected ({legacyEnvironmentProps} > {MaxLegacyEnvironmentProps}).";
                return false;
            }

            if (quaterniusDecorTrees < MinQuaterniusDecorTrees)
            {
                failureReason = $"Quaternius decorative tree scatter below minimum ({quaterniusDecorTrees} < {MinQuaterniusDecorTrees}).";
                return false;
            }

            if (quaterniusDecorVegetation < MinQuaterniusDecorVegetation)
            {
                failureReason = $"Quaternius decorative vegetation scatter below minimum ({quaterniusDecorVegetation} < {MinQuaterniusDecorVegetation}).";
                return false;
            }

            if (quaterniusDecorRocks < MinQuaterniusDecorRocks)
            {
                failureReason = $"Quaternius decorative rock scatter below minimum ({quaterniusDecorRocks} < {MinQuaterniusDecorRocks}).";
                return false;
            }

            if (quaterniusShowcaseTrees < MinQuaterniusShowcaseTrees)
            {
                failureReason = $"Grouped Quaternius tree showcase below minimum ({quaterniusShowcaseTrees} < {MinQuaterniusShowcaseTrees}).";
                return false;
            }

            if (quaterniusShowcaseVegetation < MinQuaterniusShowcaseVegetation)
            {
                failureReason = $"Grouped Quaternius vegetation showcase below minimum ({quaterniusShowcaseVegetation} < {MinQuaterniusShowcaseVegetation}).";
                return false;
            }

            if (quaterniusShowcaseRocks < MinQuaterniusShowcaseRocks)
            {
                failureReason = $"Grouped Quaternius rock showcase below minimum ({quaterniusShowcaseRocks} < {MinQuaterniusShowcaseRocks}).";
                return false;
            }

            // Validate a sample of placed objects are upright (not sideways).
            // Trees must be taller than wide; vegetation allows naturally squat plants (bushes, crops).
            int sidewaysCount = ValidateUprightSample(transforms, "Placed_QuaterniusTree_", 20, 0.5f);
            if (sidewaysCount > 0)
            {
                failureReason = $"Found {sidewaysCount} sideways Quaternius trees (height < 50% of horizontal extent). FBX axis import is broken.";
                return false;
            }

            int sidewaysVeg = ValidateUprightSample(transforms, "Placed_QuaterniusVegetation_", 20, 0.33f);
            if (sidewaysVeg > 0)
            {
                failureReason = $"Found {sidewaysVeg} sideways Quaternius vegetation (height < 33% of horizontal extent). FBX axis import is broken.";
                return false;
            }

            int sidewaysRocks = ValidateTransformUpAlignmentSample(transforms, "Placed_QuaterniusRock_", 24, 0.65f);
            if (sidewaysRocks > 0)
            {
                failureReason = $"Found {sidewaysRocks} sideways Quaternius rocks (transform up misaligned). FBX axis import is broken.";
                return false;
            }

            int sidewaysShowcaseTrees = ValidateUprightSample(transforms, "Showcase_QuaterniusTree_", 20, 0.5f);
            if (sidewaysShowcaseTrees > 0)
            {
                failureReason = $"Found {sidewaysShowcaseTrees} sideways Quaternius showcase trees (height < 50% of horizontal extent). FBX axis import is broken.";
                return false;
            }

            int sidewaysShowcaseVegetation = ValidateUprightSample(transforms, "Showcase_QuaterniusVegetation_", 20, 0.33f);
            if (sidewaysShowcaseVegetation > 0)
            {
                failureReason = $"Found {sidewaysShowcaseVegetation} sideways Quaternius showcase vegetation (height < 33% of horizontal extent). FBX axis import is broken.";
                return false;
            }

            int sidewaysShowcaseRocks = ValidateTransformUpAlignmentSample(transforms, "Showcase_QuaterniusRock_", 16, 0.65f);
            if (sidewaysShowcaseRocks > 0)
            {
                failureReason = $"Found {sidewaysShowcaseRocks} sideways Quaternius showcase rocks (transform up misaligned). FBX axis import is broken.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private static bool TryGetRenderableBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
                return false;

            bool hasBounds = false;
            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
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

        private static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        private static bool IsFinite(Quaternion q) => IsFinite(q.x) && IsFinite(q.y) && IsFinite(q.z) && IsFinite(q.w);
        private static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

        /// <summary>
        /// Samples placed objects and checks if they are upright using
        /// MeshFilter.sharedMesh.bounds transformed to world space.
        /// Renderer.bounds is unreliable in -nographics batchmode.
        /// Returns the count of sideways objects found.
        /// </summary>
        private static int ValidateUprightSample(Transform[] transforms, string namePrefix, int maxSamples, float minHeightRatio = 0.5f)
        {
            int sideways = 0;
            int checked_ = 0;

            for (int i = 0; i < transforms.Length && checked_ < maxSamples; i++)
            {
                Transform t = transforms[i];
                if (t == null || !t.name.StartsWith(namePrefix, System.StringComparison.Ordinal))
                    continue;

                Vector3 size = ComputeMeshBoundsWorldSize(t.gameObject);
                if (size.sqrMagnitude <= 0.0001f)
                    continue;

                checked_++;
                float height = size.y;
                float horizontalMax = Mathf.Max(size.x, size.z);

                if (height < horizontalMax * minHeightRatio)
                {
                    sideways++;
                    Debug.LogWarning($"[AutoTester] Sideways object: '{t.name}' meshBounds=({size.x:0.00}, {size.y:0.00}, {size.z:0.00}) threshold={minHeightRatio:0.00}");
                }
            }

            return sideways;
        }

        private static int ValidateTransformUpAlignmentSample(Transform[] transforms, string namePrefix, int maxSamples, float minUpDot)
        {
            if (transforms == null || maxSamples <= 0)
                return 0;

            int sideways = 0;
            int checkedCount = 0;

            for (int i = 0; i < transforms.Length && checkedCount < maxSamples; i++)
            {
                Transform t = transforms[i];
                if (t == null || !t.name.StartsWith(namePrefix, StringComparison.Ordinal))
                    continue;

                checkedCount++;
                float upDot = Mathf.Abs(Vector3.Dot(t.up.normalized, Vector3.up));
                if (upDot < minUpDot)
                {
                    sideways++;
                    Debug.LogWarning($"[AutoTester] Sideways transform: '{t.name}' upDot={upDot:0.00} threshold={minUpDot:0.00}");
                }
            }

            return sideways;
        }

        /// <summary>
        /// Computes world-space AABB size using MeshFilter.sharedMesh.bounds.
        /// Works in both -nographics batchmode and runtime (unlike Renderer.bounds).
        /// </summary>
        private static Vector3 ComputeMeshBoundsWorldSize(GameObject root)
        {
            if (root == null)
                return Vector3.zero;

            var meshFilters = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            if (meshFilters == null || meshFilters.Length == 0)
                return Vector3.zero;

            Bounds combined = default;
            bool hasBounds = false;

            for (int fi = 0; fi < meshFilters.Length; fi++)
            {
                var mf = meshFilters[fi];
                if (mf == null || mf.sharedMesh == null)
                    continue;

                Bounds mb = mf.sharedMesh.bounds;
                if (mb.size.sqrMagnitude <= 0.0001f)
                    continue;

                Matrix4x4 m = mf.transform.localToWorldMatrix;
                Vector3 center = mb.center;
                Vector3 ext = mb.extents;

                for (int cx = -1; cx <= 1; cx += 2)
                {
                    for (int cy = -1; cy <= 1; cy += 2)
                    {
                        for (int cz = -1; cz <= 1; cz += 2)
                        {
                            Vector3 corner = m.MultiplyPoint3x4(
                                center + new Vector3(ext.x * cx, ext.y * cy, ext.z * cz));

                            if (!hasBounds)
                            {
                                combined = new Bounds(corner, Vector3.zero);
                                hasBounds = true;
                            }
                            else
                            {
                                combined.Encapsulate(corner);
                            }
                        }
                    }
                }
            }

            return hasBounds ? combined.size : Vector3.zero;
        }
    }
}
