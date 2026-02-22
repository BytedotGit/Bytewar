using UnityEngine;
using System;
using System.Collections;
using SurvivalRPG.Networking;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Unity.Netcode.Transports.UTP;

namespace SurvivalRPG.Core
{
    /// <summary>
    /// Automatically runs a test sequence if the game is launched with the "-autoTest" argument.
    /// This allows the AI to test the game in the background without stealing window focus or simulating global OS input.
    /// </summary>
    public class AutoTester : MonoBehaviour
    {
        internal const string AutoTestArg = "-autoTest";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (HasArg(Environment.GetCommandLineArgs(), AutoTestArg))
            {
                var go = new GameObject("AutoTester");
                DontDestroyOnLoad(go);
                go.AddComponent<AutoTester>();
            }
        }

        private void Start()
        {
            Debug.Log("[AutoTester] Auto-test mode enabled. Starting test sequence...");
            StartCoroutine(TestSequence());
        }

        private IEnumerator TestSequence()
        {
            // Input sanity (compile-time + runtime). Without ENABLE_INPUT_SYSTEM, manual play is dead.
#if !ENABLE_INPUT_SYSTEM
            Debug.LogError("[AutoTester] FAIL: ENABLE_INPUT_SYSTEM is not defined. Active Input Handling is not set to New/Both.");
            yield return new WaitForSeconds(0.25f);
            Application.Quit(21);
            yield break;
#else

            // Ensure networking is running so the player spawns.
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[AutoTester] NetworkManager.Singleton is null; cannot run auto-test.");
                yield return new WaitForSeconds(1f);
                Application.Quit(2);
                yield break;
            }

            // Subscribe once: if the transport fails to start, we want that in Player.log.
            NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;

            if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
            {
                // Validate/self-repair config right before starting host.
                var boot = NetworkManager.Singleton.GetComponent<NetworkBootstrapper>();
                if (boot != null) boot.Validate();

                var config = NetworkManager.Singleton.NetworkConfig;
                string transportName = config.NetworkTransport != null ? config.NetworkTransport.GetType().Name : "NULL";
                string playerName = config.PlayerPrefab != null ? config.PlayerPrefab.name : "NULL";
                Debug.Log($"[AutoTester] Preflight: Transport={transportName} PlayerPrefab={playerName} EnableSceneMgmt={config.EnableSceneManagement} ForceSamePrefabs={config.ForceSamePrefabs}");

                var utp = config.NetworkTransport as UnityTransport;
                if (utp != null)
                {
                    Debug.Log($"[AutoTester] UnityTransport preflight: addr={utp.ConnectionData.Address} port={utp.ConnectionData.Port}");
                }

                // StartHost() can fail if the fixed port is already in use. For -autoTest we pick a high port.
                const int maxAttempts = 3;
                int seed = Environment.TickCount;
                bool ok = false;

                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    if (utp != null)
                    {
                        int port = ChooseAutoTestPort(attempt, seed);
                        utp.SetConnectionData(utp.ConnectionData.Address, (ushort)port, utp.ConnectionData.ServerListenAddress);
                        Debug.Log($"[AutoTester] Starting host (attempt {attempt + 1}/{maxAttempts}) on port {port}...");
                    }
                    else
                    {
                        Debug.Log($"[AutoTester] Starting host (attempt {attempt + 1}/{maxAttempts})...");
                    }

                    ok = NetworkManager.Singleton.StartHost();
                    Debug.Log($"[AutoTester] StartHost returned {ok} (attempt {attempt + 1}/{maxAttempts})");
                    if (ok) break;

                    // Defensive shutdown before retry.
                    try { NetworkManager.Singleton.Shutdown(); } catch { /* ignore */ }
                    yield return null;
                }

                if (!ok)
                {
                    Debug.LogError("[AutoTester] FAIL: Unable to start host after retries.");
                    yield return new WaitForSeconds(0.25f);
                    Application.Quit(34);
                    yield break;
                }
            }

            // Wait up to 15s for local player to spawn.
            float deadline = Time.realtimeSinceStartup + 15f;
            NetworkPlayer localPlayer = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                var candidate = FindFirstObjectByType<NetworkPlayer>();
                if (candidate != null && candidate.IsSpawned && candidate.IsOwner)
                {
                    localPlayer = candidate;
                    break;
                }
                yield return null;
            }

            if (localPlayer == null)
            {
                Debug.LogError("[AutoTester] Local NetworkPlayer not found/spawned within timeout.");
                yield return new WaitForSeconds(1f);
                Application.Quit(3);
                yield break;
            }

            // --- Player visual sanity (Mixamo vs fallback must be provable) ---
            var stamp = localPlayer.GeneratedVisualModeStamp;
            var actual = localPlayer.DetectActualVisualMode(out NetworkPlayer.VisualDiagnostics diag);
            Debug.Log($"[AutoTester] Visual sanity: stamp={stamp} actual={actual} child='{diag.VisualRootChildName}' skinned={diag.SkinnedMeshRendererCount} renderers={diag.RendererCount} mixamoRig={diag.HasMixamoRig}");

            if (stamp == SurvivalRPG.Networking.PlayerVisualMode.Unknown)
            {
                Debug.LogError("[AutoTester] FAIL: Player visual mode stamp is Unknown. PrefabGenerator must stamp the generated prefab so visuals can be validated.");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(28);
                yield break;
            }

            if (stamp != actual)
            {
                Debug.LogError($"[AutoTester] FAIL: Player visuals mismatch. stamp={stamp} actual={actual}.");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(29);
                yield break;
            }

            Debug.Log("[AutoTester] PASS: Player visual sanity.");

            var inputHandler = localPlayer.GetComponent<PlayerInputHandler>();
            if (inputHandler == null)
            {
                Debug.LogError("[AutoTester] PlayerInputHandler missing on local player.");
                yield return new WaitForSeconds(1f);
                Application.Quit(4);
                yield break;
            }

            // --- Animation sanity (avoid T-pose regressions) ---
            var animator = localPlayer.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[AutoTester] FAIL: Animator missing on local player.");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(30);
                yield break;
            }
            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogError("[AutoTester] FAIL: Animator.runtimeAnimatorController is null.");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(31);
                yield break;
            }
            if (animator.avatar == null)
            {
                Debug.LogError("[AutoTester] FAIL: Animator.avatar is null.");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(32);
                yield break;
            }

            Transform leftHand = null;
            if (animator.isHuman)
            {
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            }
            if (leftHand == null)
            {
                Debug.LogWarning("[AutoTester] Animation sanity: LeftHand bone not found (non-humanoid?). Falling back to clip-info check only.");
            }

            // --- Camera sanity (WoW-default framing invariants) ---
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogError("[AutoTester] FAIL: Camera.main is null.");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(23);
                yield break;
            }

            var tpc = mainCam.GetComponent<ThirdPersonCamera>();
            if (tpc == null)
            {
                Debug.LogError("[AutoTester] FAIL: ThirdPersonCamera missing on Main Camera.");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(24);
                yield break;
            }

            // Allow a couple frames for NetworkPlayer to run SetupCamera.
            yield return null;
            yield return null;

            if (tpc.Target == null)
            {
                Debug.LogError("[AutoTester] FAIL: ThirdPersonCamera.Target is null after initialization.");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(25);
                yield break;
            }

            float pivotDelta = tpc.PivotWorldPosition.y - localPlayer.transform.position.y;
            Debug.Log($"[AutoTester] Camera sanity: target='{tpc.Target.name}' pivotHeight={tpc.PivotHeight:0.00} pivotDeltaY={pivotDelta:0.00}");

            // Pivot should be around upper-chest/head, not stacked to ~3m.
            if (pivotDelta < 0.8f || pivotDelta > 2.0f)
            {
                Debug.LogError($"[AutoTester] FAIL: Camera pivot delta out of expected range. pivotDeltaY={pivotDelta:0.00}");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(26);
                yield break;
            }
            Debug.Log("[AutoTester] PASS: Camera pivot sanity.");

            // --- Grounding sanity (capsule bottom vs terrain height) ---
            var cc = localPlayer.GetComponent<CharacterController>();
            if (cc != null && Terrain.activeTerrain != null)
            {
                // Let gravity settle the capsule.
                yield return new WaitForSeconds(0.5f);

                float terrainY = Terrain.activeTerrain.SampleHeight(localPlayer.transform.position)
                               + Terrain.activeTerrain.transform.position.y;
                float capsuleBottom = localPlayer.transform.position.y + cc.center.y - (cc.height * 0.5f);
                float delta = capsuleBottom - terrainY;
                Debug.Log($"[AutoTester] Grounding sanity: capsuleBottom={capsuleBottom:0.000} terrainY={terrainY:0.000} delta={delta:0.000}");

                // Allow a small epsilon (skin width + slope).
                if (Mathf.Abs(delta) > 0.35f)
                {
                    Debug.LogError($"[AutoTester] FAIL: Player capsule not grounded within epsilon. delta={delta:0.000}");
                    yield return new WaitForSeconds(0.25f);
                    Application.Quit(27);
                    yield break;
                }
                Debug.Log("[AutoTester] PASS: Grounding sanity.");
            }
            else
            {
                Debug.LogWarning("[AutoTester] Grounding sanity skipped (no CharacterController or no active Terrain).");
            }

            // Verify real input subsystem is alive (this catches the common regression where simulated input passes).
            inputHandler.EnsureActionsEnabled("AutoTester preflight");
            bool hasKeyboard = Keyboard.current != null;
            bool hasMouse = Mouse.current != null;
            if (!hasKeyboard)
            {
                Debug.LogError("[AutoTester] FAIL: Keyboard.current is null. New Input System likely not active.");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(22);
                yield break;
            }
            if (!hasMouse)
            {
                Debug.LogWarning("[AutoTester] Mouse.current is null. Camera interaction may not work.");
            }

            // Movement verification: ensure the player's position changes after simulated input.
            Vector3 startPos = localPlayer.transform.position;
            Debug.Log($"[AutoTester] StartPos={startPos}");

            Quaternion handRotStart = leftHand != null ? leftHand.rotation : Quaternion.identity;

            Debug.Log("[AutoTester] Simulating movement input (Forward) for 1.5s...");
            inputHandler.SetSimulatedMovement(new Vector2(0, 1));
            yield return new WaitForSeconds(1.5f);

            Debug.Log("[AutoTester] Simulating movement input (Right) for 1.5s...");
            inputHandler.SetSimulatedMovement(new Vector2(1, 0));
            yield return new WaitForSeconds(1.5f);

            inputHandler.SetSimulatedMovement(Vector2.zero);
            inputHandler.ClearSimulatedMovement();

            if (leftHand != null)
            {
                float handAngle = Quaternion.Angle(handRotStart, leftHand.rotation);
                Debug.Log($"[AutoTester] Animation sanity: LeftHand angle delta={handAngle:0.00} deg");

                // If we are in a moving state, this should not be near-zero.
                if (handAngle < 1.0f)
                {
                    Debug.LogError("[AutoTester] FAIL: Animation sanity failed (hand bone did not move; likely T-pose / retarget issue).");
                    Application.Quit(33);
                    yield return new WaitForSeconds(0.25f);
                    yield break;
                }
                Debug.Log("[AutoTester] PASS: Animation sanity.");
            }

            Vector3 endPos = localPlayer.transform.position;
            float moved = Vector3.Distance(startPos, endPos);
            Debug.Log($"[AutoTester] EndPos={endPos} moved={moved:0.000}");

            if (moved > 0.25f)
            {
                Debug.Log("[AutoTester] PASS: Movement changed player position.");
                Application.Quit(0);
            }
            else
            {
                Debug.LogError("[AutoTester] FAIL: Player did not move (position delta too small).");
                Application.Quit(10);
            }

            yield return new WaitForSeconds(0.25f);

#endif
        }

        private static void OnTransportFailure()
        {
            Debug.LogError("[AutoTester] Transport failure reported by NetworkManager.OnTransportFailure.");
        }

        internal static bool HasArg(string[] args, string arg)
        {
            if (args == null || args.Length == 0) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], arg, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        internal static int ChooseAutoTestPort(int attemptIndex, int seed)
        {
            // Deterministic, collision-resistant range for automated tests.
            // Avoid well-known ports and the default 7777.
            unchecked
            {
                int mix = seed;
                mix = (mix * 397) ^ (attemptIndex * 7919);
                mix ^= (mix << 13);
                mix ^= (mix >> 17);
                mix ^= (mix << 5);
                int offset = Mathf.Abs(mix) % 10000; // 0..9999
                return 45000 + offset; // 45000..54999
            }
        }
    }
}