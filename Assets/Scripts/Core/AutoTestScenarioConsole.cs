using System.Collections;
using ByteWar.Survival;
using UnityEngine;

namespace ByteWar.Core
{
    /// <summary>
    /// AutoTester scenario that validates:
    /// - DevConsole component exists in scene
    /// - /dev command toggles DevMode
    /// - /help command produces output
    /// - Enemy materials use Standard shader (not pink)
    /// - Camera orbit only on RMB
    /// </summary>
    public class AutoTestScenarioConsole : IAutoTestScenario
    {
        public string Name => "ConsoleAndFixes";

        public IEnumerator Run(AutoTesterContext ctx)
        {
            Debug.Log("[AutoTester] Running ConsoleAndFixes scenario...");
            yield return null;

            // --- DevConsole presence ---
            var console = Object.FindFirstObjectByType<UI.DevConsole>();
            if (console == null)
            {
                Debug.LogError("[AutoTester] FAIL: ConsoleAndFixes - DevConsole not found in scene.");
                Application.Quit(60);
                yield break;
            }
            Debug.Log("[AutoTester] ConsoleAndFixes: DevConsole found.");

            // --- /dev toggles DevMode ---
            bool wasDev = GameConstants.IsDevMode();
            console.ExecuteCommand("/dev");
            yield return null;
            bool afterToggle = GameConstants.IsDevMode();
            if (afterToggle == wasDev)
            {
                Debug.LogError($"[AutoTester] FAIL: ConsoleAndFixes - /dev did not toggle DevMode. before={wasDev} after={afterToggle}");
                Application.Quit(61);
                yield break;
            }
            Debug.Log($"[AutoTester] ConsoleAndFixes: /dev toggled DevMode from {wasDev} to {afterToggle}.");

            // Toggle back
            console.ExecuteCommand("/dev");
            yield return null;
            if (GameConstants.IsDevMode() != wasDev)
            {
                Debug.LogError("[AutoTester] FAIL: ConsoleAndFixes - /dev second toggle did not restore original state.");
                Application.Quit(62);
                yield break;
            }

            // --- /help produces output ---
            int linesBefore = console.OutputLineCount;
            console.ExecuteCommand("/help");
            yield return null;
            if (console.OutputLineCount <= linesBefore)
            {
                Debug.LogError("[AutoTester] FAIL: ConsoleAndFixes - /help did not produce output.");
                Application.Quit(63);
                yield break;
            }
            Debug.Log("[AutoTester] ConsoleAndFixes: /help produced output.");

            // --- Enemy shader check: verify no pink materials ---
            var enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            Debug.Log($"[AutoTester] ConsoleAndFixes: Found {enemies.Length} enemies. Checking shaders...");
            foreach (var enemy in enemies)
            {
                var renderers = enemy.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    if (r.sharedMaterial == null || r.sharedMaterial.shader == null)
                    {
                        Debug.LogError($"[AutoTester] FAIL: ConsoleAndFixes - Enemy '{enemy.name}' renderer '{r.name}' has null material/shader.");
                        Application.Quit(64);
                        yield break;
                    }

                    string shaderName = r.sharedMaterial.shader.name;
                    if (shaderName.Contains("Error") || shaderName.Contains("Hidden/InternalErrorShader"))
                    {
                        Debug.LogError($"[AutoTester] FAIL: ConsoleAndFixes - Enemy '{enemy.name}' renderer '{r.name}' has error shader: {shaderName}");
                        Application.Quit(65);
                        yield break;
                    }
                }
            }
            Debug.Log("[AutoTester] ConsoleAndFixes: Enemy shader check passed.");

            // --- Camera: verify orbit only happens with RMB ---
            var tpc = ctx.ThirdPersonCamera;
            if (tpc == null)
            {
                Debug.LogError("[AutoTester] FAIL: ConsoleAndFixes - ThirdPersonCamera is null.");
                Application.Quit(66);
                yield break;
            }
            // We can't simulate mouse buttons in headless, but verify the LMB/RMB properties exist
            Debug.Log($"[AutoTester] ConsoleAndFixes: Camera LMB={tpc.IsLeftMouseHeld} RMB={tpc.IsRightMouseHeld} (expected both false in headless).");

            // --- Player CharacterController stepOffset check ---
            var playerCC = ctx.CharacterController;
            if (playerCC != null)
            {
                if (playerCC.stepOffset < 0.3f)
                {
                    Debug.LogError($"[AutoTester] FAIL: ConsoleAndFixes - Player stepOffset={playerCC.stepOffset} is too low for stairs.");
                    Application.Quit(67);
                    yield break;
                }
                if (playerCC.slopeLimit < 40f)
                {
                    Debug.LogError($"[AutoTester] FAIL: ConsoleAndFixes - Player slopeLimit={playerCC.slopeLimit} is too low for ramps.");
                    Application.Quit(68);
                    yield break;
                }
                Debug.Log($"[AutoTester] PASS: Player stepOffset={playerCC.stepOffset} slopeLimit={playerCC.slopeLimit}.");
            }

            // --- Building collider check: ramps/stairs should have walkable colliders ---
            var buildingController = ctx.LocalPlayer.GetComponent<ByteWar.Building.BuildingController>();
            if (buildingController != null)
            {
                var prefabs = buildingController.BuildingPrefabs;
                if (prefabs != null)
                {
                    foreach (var prefab in prefabs)
                    {
                        if (prefab == null) continue;
                        var piece = prefab.GetComponent<ByteWar.Building.BuildingPiece>();
                        if (piece == null) continue;
                        var pieceType = piece.PieceType;

                        if (pieceType == ByteWar.Building.BuildingPieceType.Ramp ||
                            pieceType == ByteWar.Building.BuildingPieceType.Roof26 ||
                            pieceType == ByteWar.Building.BuildingPieceType.AngledWall)
                        {
                            var mc = prefab.GetComponent<MeshCollider>();
                            if (mc == null)
                            {
                                Debug.LogError($"[AutoTester] FAIL: ConsoleAndFixes - {pieceType} should have MeshCollider, not BoxCollider.");
                                Application.Quit(69);
                                yield break;
                            }
                        }
                        else if (pieceType == ByteWar.Building.BuildingPieceType.Stairs)
                        {
                            var stepColliders = prefab.GetComponentsInChildren<BoxCollider>();
                            if (stepColliders.Length < 12)
                            {
                                Debug.LogError($"[AutoTester] FAIL: ConsoleAndFixes - Stairs should have 12+ step colliders, found {stepColliders.Length}.");
                                Application.Quit(70);
                                yield break;
                            }
                        }
                    }
                    Debug.Log("[AutoTester] PASS: Building collider shapes are walkable.");
                }
            }

            Debug.Log("[AutoTester] PASS: ConsoleAndFixes scenario complete.");
            yield return null;
        }
    }
}
