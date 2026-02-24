using System.Collections;
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
        public string Name => "Core";

        public IEnumerator Run(AutoTesterContext ctx)
        {
            var localPlayer = ctx.LocalPlayer;
            var inputHandler = ctx.InputHandler;
            var mainCam = ctx.MainCamera;
            var tpc = ctx.ThirdPersonCamera;
            var animator = ctx.Animator;

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

            Vector3 afterTurnPos = localPlayer.transform.position;
            float movedDuringTurn = Vector3.Distance(afterForwardPos, afterTurnPos);
            Debug.Log($"[AutoTester] Turn sanity: movedDuringTurn={movedDuringTurn:0.000}");

            if (yawDelta < 5f)
            {
                Debug.LogError("[AutoTester] FAIL: Core - Expected A/D input to turn player when RMB is not held.");
                Application.Quit(40);
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

        private static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        private static bool IsFinite(Quaternion q) => IsFinite(q.x) && IsFinite(q.y) && IsFinite(q.z) && IsFinite(q.w);
        private static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
