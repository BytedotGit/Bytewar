using UnityEngine;
using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using ByteWar.Networking;
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

            // --- Build context for scenarios ---
            var inputHandler = localPlayer.GetComponent<PlayerInputHandler>();
            if (inputHandler == null)
            {
                Debug.LogError("[AutoTester] PlayerInputHandler missing on local player.");
                yield return new WaitForSeconds(1f);
                Application.Quit(4);
                yield break;
            }

            var animator = localPlayer.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[AutoTester] FAIL: Animator missing on local player.");
                yield return new WaitForSeconds(0.25f);
                Application.Quit(30);
                yield break;
            }

            // Allow a couple frames for NetworkPlayer to run SetupCamera.
            yield return null;
            yield return null;

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

            var ctx = new AutoTesterContext
            {
                LocalPlayer = localPlayer,
                InputHandler = inputHandler,
                MainCamera = mainCam,
                ThirdPersonCamera = tpc,
                Animator = animator,
                CharacterController = localPlayer.GetComponent<CharacterController>(),
                NetworkManager = NetworkManager.Singleton,
            };

            // --- Determine which scenarios to run ---
            var scenarios = BuildScenarioList(Environment.GetCommandLineArgs());

            Debug.Log($"[AutoTester] Running {scenarios.Length} scenario(s): {string.Join(", ", System.Array.ConvertAll(scenarios, s => s.Name))}");

            foreach (var scenario in scenarios)
            {
                Debug.Log($"[AutoTester] === Starting scenario: {scenario.Name} ===");
                yield return StartCoroutine(scenario.Run(ctx));
            }

            Debug.Log("[AutoTester] All scenarios complete. Exiting with code 0.");
            Application.Quit(0);
            yield return new WaitForSeconds(0.25f);

#endif
        }

        private void HandleTransportFailure()
        {
            _transportFailureCount++;
            _lastTransportFailureRealtime = Time.realtimeSinceStartup;
            Debug.LogError($"[AutoTester] Transport failure reported by NetworkManager.OnTransportFailure (count={_transportFailureCount}).");
        }

        // ── Scenario management ───────────────────────────────────────────────

        /// <summary>All registered scenario factories. Add new scenarios here.</summary>
        private static readonly System.Func<IAutoTestScenario>[] AllScenarioFactories =
        {
            () => new AutoTestScenarioCore(),
            () => new AutoTestScenarioBuildingVisuals(),
            () => new AutoTestScenarioConsole(),
            () => new AutoTestScenarioBlenderE2EProp(),
            () => new AutoTestScenarioAssetDeployUI(),
        };

        /// <summary>
        /// Builds the list of scenarios to run based on command-line args.
        /// If no -autoTestScenario args are specified, ALL scenarios run.
        /// </summary>
        internal static IAutoTestScenario[] BuildScenarioList(string[] args)
        {
            var requestedNames = ParseScenarioArgs(args);

            if (requestedNames.Count == 0)
            {
                // No specific scenarios requested → run all
                var all = new IAutoTestScenario[AllScenarioFactories.Length];
                for (int i = 0; i < AllScenarioFactories.Length; i++)
                    all[i] = AllScenarioFactories[i]();
                return all;
            }

            var result = new System.Collections.Generic.List<IAutoTestScenario>();
            foreach (var factory in AllScenarioFactories)
            {
                var scenario = factory();
                if (requestedNames.Contains(scenario.Name))
                    result.Add(scenario);
            }

            if (result.Count == 0)
            {
                Debug.LogWarning($"[AutoTester] No matching scenarios found for requested names. Running all.");
                var all = new IAutoTestScenario[AllScenarioFactories.Length];
                for (int i = 0; i < AllScenarioFactories.Length; i++)
                    all[i] = AllScenarioFactories[i]();
                return all;
            }

            return result.ToArray();
        }

        /// <summary>
        /// Parses -autoTestScenario arguments from the command line.
        /// Example: -autoTest -autoTestScenario Core -autoTestScenario BuildingVisuals
        /// </summary>
        internal static System.Collections.Generic.HashSet<string> ParseScenarioArgs(string[] args)
        {
            var names = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (args == null) return names;

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-autoTestScenario", StringComparison.OrdinalIgnoreCase))
                {
                    names.Add(args[i + 1]);
                    i++; // skip the value
                }
            }
            return names;
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