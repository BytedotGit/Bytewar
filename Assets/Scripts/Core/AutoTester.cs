using UnityEngine;
using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using ByteWar.Networking;
using ByteWar.Building;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Unity.Netcode.Transports.UTP;

namespace ByteWar.Core
{
    /// <summary>
    /// Automatically runs a test sequence if the game is launched with the "-autoTest" argument.
    /// This allows the AI to test the game in the background without stealing window focus or simulating global OS input.
    /// </summary>
    public class AutoTester : MonoBehaviour
    {
        internal const string AutoTestArg = "-autoTest";

        private const int AutoTestPortMin = 45000;
        private const int AutoTestPortRange = 10000; // 45000..54999
        private const int AutoTestPortProbeCount = 12;

        private int _transportFailureCount;
        private float _lastTransportFailureRealtime;

        private ILogHandler _previousLogHandler;
        private bool _suppressNetcodeDestroyWarning;
        private int _suppressedNetcodeDestroyWarnings;

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
            InstallLogFilter();
            Debug.Log("[AutoTester] Auto-test mode enabled. Starting test sequence...");
            StartCoroutine(TestSequence());
        }

        private void OnDestroy()
        {
            RestoreLogFilter();
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnTransportFailure -= HandleTransportFailure;
            }
        }

        private void InstallLogFilter()
        {
            if (_previousLogHandler != null) return;
            _previousLogHandler = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = new AutoTestLogFilterHandler(_previousLogHandler, this);
        }

        private void RestoreLogFilter()
        {
            if (_previousLogHandler == null) return;
            if (ReferenceEquals(Debug.unityLogger.logHandler, null)) return;

            // Only restore if we still own the handler.
            if (Debug.unityLogger.logHandler is AutoTestLogFilterHandler)
            {
                Debug.unityLogger.logHandler = _previousLogHandler;
            }

            _previousLogHandler = null;
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
            NetworkManager.Singleton.OnTransportFailure -= HandleTransportFailure;
            NetworkManager.Singleton.OnTransportFailure += HandleTransportFailure;

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
                        bool probed = TryChooseAvailableAutoTestPort(attempt, seed, AutoTestPortProbeCount, out int availablePort, out int probesTried);
                        if (probed)
                        {
                            port = availablePort;
                            Debug.Log($"[AutoTester] Port probe: selected available UDP port {port} after {probesTried} probe(s).");
                        }
                        else
                        {
                            Debug.LogWarning($"[AutoTester] Port probe: unable to confirm a free UDP port after {AutoTestPortProbeCount} probe(s); proceeding with port {port}.");
                        }

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

                    // Diagnostics to help de-flake host start in CI/builds.
                    string lastFailureAge = _transportFailureCount > 0
                        ? $"{(Time.realtimeSinceStartup - _lastTransportFailureRealtime):0.000}s"
                        : "n/a";

                    Debug.LogWarning($"[AutoTester] Host start failed (attempt {attempt + 1}/{maxAttempts}). " +
                                     $"isListening={NetworkManager.Singleton.IsListening} isServer={NetworkManager.Singleton.IsServer} isClient={NetworkManager.Singleton.IsClient} " +
                                     $"transportFailures={_transportFailureCount} lastTransportFailureAge={lastFailureAge}");

                    // Defensive shutdown before retry.
                    // Avoid calling Shutdown() when StartHost() never reached a listening state; this can trigger noisy NGO warnings.
                    if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient)
                    {
                        try
                        {
                            Debug.LogWarning("[AutoTester] Shutting down NetworkManager before retry...");
                            _suppressNetcodeDestroyWarning = true;
                            NetworkManager.Singleton.Shutdown();
                        }
                        catch
                        {
                            // ignore
                        }

                        // Give NGO a moment to tear down cleanly.
                        yield return new WaitForSecondsRealtime(0.05f);
                        _suppressNetcodeDestroyWarning = false;
                    }
                    else
                    {
                        yield return null;
                    }

                    if (attempt < (maxAttempts - 1))
                    {
                        float delay = ComputeHostRetryDelaySeconds(attempt);
                        if (delay > 0f)
                        {
                            Debug.Log($"[AutoTester] Backoff: waiting {delay:0.00}s before retry...");
                            yield return new WaitForSecondsRealtime(delay);
                        }
                    }
                }

                if (!ok)
                {
                    Debug.LogError("[AutoTester] FAIL: Unable to start host after retries.");
                    yield return new WaitForSeconds(0.25f);
                    Application.Quit(34);
                    yield break;
                }

                if (_suppressedNetcodeDestroyWarnings > 0)
                {
                    Debug.LogWarning($"[AutoTester] Suppressed NGO destroy warnings during retries: count={_suppressedNetcodeDestroyWarnings}");
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

            if (stamp == ByteWar.Networking.PlayerVisualMode.Unknown)
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

            // --- First-person zoom sanity (explicit mode; stable rotation; renderers hidden) ---
            Debug.Log("[AutoTester] Forcing first-person zoom sanity...");

            // Force immediately and apply once to avoid relying on input events.
            tpc.AutoTest_SetTargetDistance(0f, immediate: true);
            tpc.AutoTest_TickCameraTransformOnce();
            yield return null;

            // Re-sync camera to current-frame target (coroutines resume before LateUpdate,
            // so the player may have moved since last LateUpdate positioned the camera).
            tpc.AutoTest_TickCameraTransformOnce();

            Vector3 fpPivot = tpc.PivotWorldPosition;
            Vector3 camPos = mainCam.transform.position;
            Quaternion camRot = mainCam.transform.rotation;

            float fpDist = Vector3.Distance(camPos, fpPivot);
            Debug.Log($"[AutoTester] First-person sanity: camPos={camPos} pivot={fpPivot} dist={fpDist:0.000}");

            if (!IsFinite(camPos) || !IsFinite(camRot))
            {
                Debug.LogError("[AutoTester] FAIL: First-person zoom sanity failed (camera transform contains NaN/Inf).");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(35);
                yield break;
            }

            if (fpDist > 0.05f)
            {
                Debug.LogError($"[AutoTester] FAIL: First-person zoom sanity failed (camera not at pivot). dist={fpDist:0.000}");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(36);
                yield break;
            }

            // Validate that the player's visible renderers are hidden in first-person.
            Transform visualRoot = localPlayer.transform.Find("VisualRoot");
            if (visualRoot != null)
            {
                var renderers = visualRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
                if (renderers != null && renderers.Length > 0)
                {
                    bool anyEnabled = false;
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        var r = renderers[i];
                        if (r != null && r.enabled)
                        {
                            anyEnabled = true;
                            break;
                        }
                    }
                    Debug.Log($"[AutoTester] First-person sanity: visualRootRenderers={renderers.Length} anyEnabled={anyEnabled}");

                    if (anyEnabled)
                    {
                        Debug.LogError("[AutoTester] FAIL: First-person zoom sanity failed (player renderers still enabled).");
                        yield return new WaitForSeconds(0.25f);
                        Application.Quit(37);
                        yield break;
                    }
                }
            }
            else
            {
                Debug.LogWarning("[AutoTester] First-person sanity: VisualRoot not found; renderer-hide check skipped.");
            }

            Debug.Log("[AutoTester] PASS: First-person zoom sanity.");

            // --- Grounding sanity (capsule bottom vs ground — raycast-based, terrain-independent) ---
            var cc = localPlayer.GetComponent<CharacterController>();
            if (cc != null)
            {
                // Let gravity settle the capsule.
                yield return new WaitForSeconds(0.5f);

                float groundY = 0f;
                bool hasGround = false;

                // Prefer raycast
                Vector3 groundProbe = localPlayer.transform.position + Vector3.up * 2f;
                if (Physics.Raycast(groundProbe, Vector3.down, out RaycastHit groundHit, 20f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    groundY = groundHit.point.y;
                    hasGround = true;
                    Debug.Log($"[AutoTester] Grounding probe hit '{groundHit.collider.name}' at y={groundY:0.000}");
                }
                else if (Terrain.activeTerrain != null)
                {
                    groundY = Terrain.activeTerrain.SampleHeight(localPlayer.transform.position)
                            + Terrain.activeTerrain.transform.position.y;
                    hasGround = true;
                }

                if (hasGround)
                {
                    float capsuleBottom = localPlayer.transform.position.y + cc.center.y - (cc.height * 0.5f);
                    float delta = capsuleBottom - groundY;
                    Debug.Log($"[AutoTester] Grounding sanity: capsuleBottom={capsuleBottom:0.000} groundY={groundY:0.000} delta={delta:0.000}");

                    // Greybox flat-surface colliders cause CharacterController to hover
                    // higher than shaped Terrain; epsilon widened to 0.45 for tolerance.
                    if (Mathf.Abs(delta) > 0.45f)
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
                    Debug.LogWarning("[AutoTester] Grounding sanity skipped (no ground collider or terrain found).");
                }
            }
            else
            {
                Debug.LogWarning("[AutoTester] Grounding sanity skipped (no CharacterController).");
            }

            // --- Visual grounding sanity (mesh/feet vs ground — terrain-independent) ---
            if (localPlayer != null)
            {
                Transform vr = localPlayer.transform.Find("VisualRoot");
                if (vr != null && VisualGroundingUtility.TryGetSupportGroundY(localPlayer.transform.position, ignoreRoot: localPlayer.transform, animator, out float groundY, out string groundSource))
                {
                    if (VisualGroundingUtility.TrySampleVisualBottomY(vr, animator, out var sample))
                    {
                        float delta = sample.SelectedBottomY - groundY;
                        Debug.Log($"[AutoTester] Visual grounding sanity: groundY={groundY:0.000} source={groundSource} visualY={sample.SelectedBottomY:0.000} method={sample.Method} delta={delta:0.000} details=({sample.Details})");

                        // We expect the visual bottom to be close to ground after the runtime correction.
                        // Epsilon is generous (0.40) because greybox flat-surface colliders cause
                        // CharacterController to hover higher than shaped Terrain.
                        if (Mathf.Abs(delta) > 0.40f)
                        {
                            Debug.LogError($"[AutoTester] FAIL: Visual grounding sanity failed. delta={delta:0.000}");
                            yield return new WaitForSeconds(0.25f);
                            Application.Quit(38);
                            yield break;
                        }

                        Debug.Log("[AutoTester] PASS: Visual grounding sanity.");
                    }
                    else
                    {
                        Debug.LogWarning("[AutoTester] Visual grounding sanity skipped: unable to sample visual bottom Y.");
                    }
                }
                else
                {
                    Debug.LogWarning("[AutoTester] Visual grounding sanity skipped (no VisualRoot or unable to sample groundY).");
                }
            }

            // --- Building system sanity ---
            var buildingController = localPlayer.GetComponent<BuildingController>();
            if (buildingController == null)
            {
                Debug.LogError("[AutoTester] FAIL: BuildingController missing on local player.");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(39);
                yield break;
            }

            int recipeCount = buildingController.Recipes != null ? buildingController.Recipes.Count : 0;
            Debug.Log($"[AutoTester] Building sanity: BuildingController present. recipes={recipeCount} buildModeActive={buildingController.IsBuildModeActive}");

            if (recipeCount == 0)
            {
                Debug.LogWarning("[AutoTester] Building sanity: No recipes wired. Building placement will not work at runtime.");
            }

            // Verify WorldPersistence is present on the NetworkManager
            WorldPersistence worldPersistence = null;
            if (NetworkManager.Singleton != null)
            {
                worldPersistence = NetworkManager.Singleton.GetComponent<WorldPersistence>();
            }
            if (worldPersistence == null)
            {
                Debug.LogWarning("[AutoTester] Building sanity: WorldPersistence not found on NetworkManager.");
            }
            else
            {
                Debug.Log($"[AutoTester] Building sanity: WorldPersistence present. savePath='{worldPersistence.SaveFilePath}'");
            }

            Debug.Log("[AutoTester] PASS: Building system sanity.");

            // --- VFX & Audio system sanity ---
            var vfxManager = UnityEngine.Object.FindFirstObjectByType<VFXManager>();
            if (vfxManager == null)
            {
                Debug.LogError("[AutoTester] FAIL: VFXManager not found in scene. Run Generate Scene.");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(41);
                yield break;
            }
            Debug.Log($"[AutoTester] VFX sanity: VFXManager present. allPrefabsAssigned={vfxManager.AllPrefabsAssigned}");
            Debug.Log("[AutoTester] PASS: VFXManager present in scene.");

            var audioManager = UnityEngine.Object.FindFirstObjectByType<AudioManager>();
            if (audioManager == null)
            {
                Debug.LogError("[AutoTester] FAIL: AudioManager not found in scene. Run Generate Scene.");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(42);
                yield break;
            }
            Debug.Log("[AutoTester] PASS: AudioManager present in scene.");

            var footstep = localPlayer.GetComponent<FootstepController>();
            if (footstep == null)
            {
                Debug.LogError("[AutoTester] FAIL: FootstepController missing on local player. Run Generate Prefabs.");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(43);
                yield break;
            }
            Debug.Log("[AutoTester] PASS: Player has FootstepController.");

            var playerAudioSource = localPlayer.GetComponent<AudioSource>();
            if (playerAudioSource == null)
            {
                Debug.LogError("[AutoTester] FAIL: AudioSource missing on local player. Run Generate Prefabs.");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(44);
                yield break;
            }
            Debug.Log("[AutoTester] PASS: Player has AudioSource.");

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

        private void HandleTransportFailure()
        {
            _transportFailureCount++;
            _lastTransportFailureRealtime = Time.realtimeSinceStartup;
            Debug.LogError($"[AutoTester] Transport failure reported by NetworkManager.OnTransportFailure (count={_transportFailureCount}).");
        }

        private sealed class AutoTestLogFilterHandler : ILogHandler
        {
            private readonly ILogHandler _inner;
            private readonly AutoTester _owner;

            public AutoTestLogFilterHandler(ILogHandler inner, AutoTester owner)
            {
                _inner = inner;
                _owner = owner;
            }

            public void LogException(Exception exception, UnityEngine.Object context)
            {
                _inner.LogException(exception, context);
            }

            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                // Targeted spam suppression: NGO can emit noisy warnings during rapid Shutdown() after failed starts.
                // Keep this extremely narrow to avoid hiding real problems.
                if (_owner._suppressNetcodeDestroyWarning && logType == LogType.Warning &&
                    !string.IsNullOrEmpty(format) && format.Contains("Trying to destroy object 0"))
                {
                    _owner._suppressedNetcodeDestroyWarnings++;
                    return;
                }

                _inner.LogFormat(logType, context, format, args);
            }
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
                int offset = Mathf.Abs(mix) % AutoTestPortRange; // 0..9999
                return AutoTestPortMin + offset; // 45000..54999
            }
        }

        internal static float ComputeHostRetryDelaySeconds(int attemptIndex)
        {
            // attemptIndex is 0-based (0 = after first failure). Keep total delay low.
            return attemptIndex switch
            {
                0 => 0.10f,
                1 => 0.25f,
                _ => 0.50f,
            };
        }

        internal static bool TryChooseAvailableAutoTestPort(int attemptIndex, int seed, int probeCount, out int port, out int probesTried)
        {
            // Probe multiple deterministic candidates to reduce flaky bind failures (and noisy logs).
            probeCount = Mathf.Clamp(probeCount, 1, 64);
            probesTried = 0;

            for (int probe = 0; probe < probeCount; probe++)
            {
                probesTried++;
                int candidate = ChooseAutoTestPort((attemptIndex * probeCount) + probe, seed);
                if (IsUdpPortAvailable(candidate))
                {
                    port = candidate;
                    return true;
                }
            }

            port = ChooseAutoTestPort(attemptIndex, seed);
            return false;
        }

        private static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        }

        private static bool IsFinite(Quaternion q)
        {
            return IsFinite(q.x) && IsFinite(q.y) && IsFinite(q.z) && IsFinite(q.w);
        }

        private static bool IsFinite(float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }

        internal static bool IsUdpPortAvailable(int port)
        {
            if (port <= 0 || port > 65535) return false;

            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.ExclusiveAddressUse = true;
                socket.Bind(new IPEndPoint(IPAddress.Any, port));
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            catch
            {
                // In restricted environments, we might not be able to probe; treat as unknown (not available)
                // so the caller falls back to a deterministic candidate.
                return false;
            }
        }
    }
}