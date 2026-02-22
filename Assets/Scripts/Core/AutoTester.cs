using UnityEngine;
using System;
using System.Collections;
using SurvivalRPG.Networking;
using Unity.Netcode;
using UnityEngine.InputSystem;

namespace SurvivalRPG.Core
{
    /// <summary>
    /// Automatically runs a test sequence if the game is launched with the "-autoTest" argument.
    /// This allows the AI to test the game in the background without stealing window focus or simulating global OS input.
    /// </summary>
    public class AutoTester : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "-autoTest"))
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

            if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
            {
                Debug.Log("[AutoTester] Starting host...");
                bool ok = NetworkManager.Singleton.StartHost();
                Debug.Log($"[AutoTester] StartHost returned {ok}");
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

            var inputHandler = localPlayer.GetComponent<PlayerInputHandler>();
            if (inputHandler == null)
            {
                Debug.LogError("[AutoTester] PlayerInputHandler missing on local player.");
                yield return new WaitForSeconds(1f);
                Application.Quit(4);
                yield break;
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

            Debug.Log("[AutoTester] Simulating movement input (Forward) for 1.5s...");
            inputHandler.SetSimulatedMovement(new Vector2(0, 1));
            yield return new WaitForSeconds(1.5f);

            Debug.Log("[AutoTester] Simulating movement input (Right) for 1.5s...");
            inputHandler.SetSimulatedMovement(new Vector2(1, 0));
            yield return new WaitForSeconds(1.5f);

            inputHandler.SetSimulatedMovement(Vector2.zero);
            inputHandler.ClearSimulatedMovement();

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
    }
}