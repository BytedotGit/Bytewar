using UnityEngine;
using UnityEngine.InputSystem;

namespace ByteWar.Core
{
    public class PlayerInputHandler : MonoBehaviour
    {
        // InputAction instances retained for rebinding support / test access.
        public InputAction MovementAction { get; private set; }
        public InputAction CastSpell1Action { get; private set; }
        public InputAction CastSpell2Action { get; private set; }
        public InputAction InteractAction { get; private set; }
        public InputAction BuildAction { get; private set; }
        public InputAction JumpAction { get; private set; }

        public Vector2 MovementInput { get; private set; }
        public bool InteractTriggered { get; private set; }
        public bool CastSpell1Triggered { get; private set; }
        public bool CastSpell2Triggered { get; private set; }
        public bool BuildTriggered { get; private set; }
        public bool JumpTriggered { get; private set; }

        /// <summary>When true, all gameplay input is suppressed (e.g., console is open).</summary>
        public bool InputSuppressed { get; set; }

        private float _nextMoveLogTime;
        private Vector2 _lastLoggedMove;
        private bool _isSimulated;

        private void Awake()
        {
            Debug.Log($"[{nameof(PlayerInputHandler)}] Initializing input (direct keyboard polling).");

#if !ENABLE_INPUT_SYSTEM
            Debug.LogError($"[{nameof(PlayerInputHandler)}] ENABLE_INPUT_SYSTEM is NOT defined. " +
                           "Project is likely set to 'Old Input Manager' only. " +
                           "Fix: ProjectSettings -> Active Input Handling = Both (or New). Movement/camera will not work otherwise.");
#endif

            Application.runInBackground = true;

            // Create InputAction instances (for test access / future rebinding).
            // These are NOT used for runtime polling — we read Keyboard.current directly
            // because standalone InputAction instances can silently fail binding resolution
            // in builds, while Keyboard.current always works (matches BuildingController,
            // ThirdPersonCamera, ScreenLogger, and KeybindingHUD patterns).
            MovementAction = new InputAction("Movement", type: InputActionType.Value, expectedControlType: "Vector2");
            MovementAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            CastSpell1Action = new InputAction("CastSpell1", type: InputActionType.Button, binding: "<Keyboard>/1");
            CastSpell2Action = new InputAction("CastSpell2", type: InputActionType.Button, binding: "<Keyboard>/2");
            InteractAction = new InputAction("Interact", type: InputActionType.Button, binding: "<Keyboard>/e");
            BuildAction = new InputAction("Build", type: InputActionType.Button, binding: "<Keyboard>/b");
            JumpAction = new InputAction("Jump", type: InputActionType.Button, binding: "<Keyboard>/space");

            Debug.Log($"[{nameof(PlayerInputHandler)}] Devices: Keyboard={(Keyboard.current != null)} Mouse={(Mouse.current != null)}");
        }

        private void OnEnable()
        {
            Debug.Log($"[{nameof(PlayerInputHandler)}] Enabling Input Actions.");
            EnsureActionsEnabled("OnEnable");
        }

        private void OnDisable()
        {
            Debug.Log($"[{nameof(PlayerInputHandler)}] Disabling Input Actions.");
            if (MovementAction != null) MovementAction.Disable();
            if (CastSpell1Action != null) CastSpell1Action.Disable();
            if (CastSpell2Action != null) CastSpell2Action.Disable();
            if (InteractAction != null) InteractAction.Disable();
            if (BuildAction != null) BuildAction.Disable();
            if (JumpAction != null) JumpAction.Disable();
        }

        private void Update()
        {
            // Suppress all gameplay input when console or other overlay is active
            if (InputSuppressed)
            {
                MovementInput = Vector2.zero;
                InteractTriggered = false;
                CastSpell1Triggered = false;
                CastSpell2Triggered = false;
                BuildTriggered = false;
                JumpTriggered = false;
                return;
            }

            if (!_isSimulated)
            {
                PollKeyboard();
            }

            // Throttle logs to avoid per-frame spam.
            if (Time.unscaledTime >= _nextMoveLogTime)
            {
                bool changed = (MovementInput - _lastLoggedMove).sqrMagnitude > 0.01f;
                if (changed)
                {
                    Debug.Log($"[PlayerInputHandler] MovementInput: {MovementInput}");
                    _lastLoggedMove = MovementInput;
                }
                _nextMoveLogTime = Time.unscaledTime + 0.5f;
            }
        }

        /// <summary>
        /// Polls Keyboard.current directly for all gameplay input.
        /// This mirrors the pattern used by BuildingController, ThirdPersonCamera,
        /// ScreenLogger, and KeybindingHUD, which all read device state directly.
        /// </summary>
        private void PollKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                MovementInput = Vector2.zero;
                return;
            }

            // ── Movement (WASD + Arrows + ZQSD) ────────────────────────────────
            float x = 0f, y = 0f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed || kb.zKey.isPressed) y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed || kb.qKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;

            Vector2 raw = new Vector2(x, y);
            MovementInput = raw.sqrMagnitude > 1f ? raw.normalized : raw;

            // ── Button triggers (wasPressedThisFrame for one-shot actions) ──────
            if (kb.eKey.wasPressedThisFrame) InteractTriggered = true;
            if (kb.digit1Key.wasPressedThisFrame) CastSpell1Triggered = true;
            if (kb.digit2Key.wasPressedThisFrame) CastSpell2Triggered = true;
            if (kb.bKey.wasPressedThisFrame) BuildTriggered = true;
            if (kb.spaceKey.wasPressedThisFrame) JumpTriggered = true;
        }

        // Helper to consume triggers so they don't fire multiple times per frame
        public bool ConsumeInteract()
        {
            if (InteractTriggered)
            {
                InteractTriggered = false;
                return true;
            }
            return false;
        }

        public bool ConsumeCastSpell1()
        {
            if (CastSpell1Triggered)
            {
                CastSpell1Triggered = false;
                return true;
            }
            return false;
        }

        public bool ConsumeCastSpell2()
        {
            if (CastSpell2Triggered)
            {
                CastSpell2Triggered = false;
                return true;
            }
            return false;
        }

        public bool ConsumeBuild()
        {
            if (BuildTriggered)
            {
                BuildTriggered = false;
                return true;
            }
            return false;
        }

        public bool ConsumeJump()
        {
            if (JumpTriggered)
            {
                JumpTriggered = false;
                return true;
            }
            return false;
        }

        public void SetSimulatedMovement(Vector2 input)
        {
            _isSimulated = true;
            MovementInput = input;
        }

        public void ClearSimulatedMovement()
        {
            _isSimulated = false;
        }

        public void SimulateInteractPress()
        {
            InteractTriggered = true;
        }

        public void SimulateJumpPress()
        {
            JumpTriggered = true;
        }

        public void EnsureActionsEnabled(string context)
        {
            if (MovementAction == null)
            {
                Debug.LogError($"[{nameof(PlayerInputHandler)}] EnsureActionsEnabled called but MovementAction is null (context={context}).");
                return;
            }

            try
            {
                if (!MovementAction.enabled) MovementAction.Enable();
                if (!CastSpell1Action.enabled) CastSpell1Action.Enable();
                if (!CastSpell2Action.enabled) CastSpell2Action.Enable();
                if (!InteractAction.enabled) InteractAction.Enable();
                if (!BuildAction.enabled) BuildAction.Enable();
                if (!JumpAction.enabled) JumpAction.Enable();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[{nameof(PlayerInputHandler)}] Failed enabling InputActions (context={context}): {ex}");
            }

            Debug.Log($"[{nameof(PlayerInputHandler)}] InputActions enabled (context={context}). " +
                      $"Movement={MovementAction.enabled} Cast1={CastSpell1Action.enabled} Interact={InteractAction.enabled} " +
                      $"Keyboard={(Keyboard.current != null)} Mouse={(Mouse.current != null)}");
        }

        /// <summary>
        /// Force disable/re-enable all InputActions to recover from IMGUI
        /// TextField stealing keyboard focus from the Input System.
        /// </summary>
        public void ForceResetActions()
        {
            if (MovementAction == null) return;

            try
            {
                MovementAction.Disable();
                CastSpell1Action.Disable();
                CastSpell2Action.Disable();
                InteractAction.Disable();
                BuildAction.Disable();
                JumpAction.Disable();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[{nameof(PlayerInputHandler)}] ForceResetActions disable error: {ex.Message}");
            }

            EnsureActionsEnabled("ForceResetActions");
        }
    }
}
