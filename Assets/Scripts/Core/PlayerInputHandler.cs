using UnityEngine;
using UnityEngine.InputSystem;

namespace ByteWar.Core
{
    public class PlayerInputHandler : MonoBehaviour
    {
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

        private float _nextMoveLogTime;
        private Vector2 _lastLoggedMove;

        private void Awake()
        {
            Debug.Log($"[{nameof(PlayerInputHandler)}] Initializing Input Actions.");

#if !ENABLE_INPUT_SYSTEM
            Debug.LogError($"[{nameof(PlayerInputHandler)}] ENABLE_INPUT_SYSTEM is NOT defined. " +
                           "Project is likely set to 'Old Input Manager' only. " +
                           "Fix: ProjectSettings -> Active Input Handling = Both (or New). Movement/camera will not work otherwise.");
#endif

            // Development-friendly defaults: keep input alive even if focus changes.
            // This reduces "can't move" reports when the game window isn't focused.
            // (Standalone builds only care about responsiveness; this can be revisited later.)
            Application.runInBackground = true;
            try
            {
                if (InputSystem.settings != null)
                {
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[{nameof(PlayerInputHandler)}] Failed to set InputSystem backgroundBehavior: {ex.Message}");
            }

            // Define actions in code
            MovementAction = new InputAction("Movement", type: InputActionType.Value, expectedControlType: "Vector2");
            MovementAction.AddBinding("<Gamepad>/leftStick");

            // Primary: WASD
            MovementAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            // Fallback: Arrow keys
            MovementAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            // Fallback: AZERTY-style movement (ZQSD)
            MovementAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/z")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/q")
                .With("Right", "<Keyboard>/d");

            CastSpell1Action = new InputAction("CastSpell1", type: InputActionType.Button, binding: "<Keyboard>/1");
            CastSpell2Action = new InputAction("CastSpell2", type: InputActionType.Button, binding: "<Keyboard>/2");
            InteractAction = new InputAction("Interact", type: InputActionType.Button, binding: "<Keyboard>/e");
            BuildAction = new InputAction("Build", type: InputActionType.Button, binding: "<Keyboard>/b");
            JumpAction = new InputAction("Jump", type: InputActionType.Button, binding: "<Keyboard>/space");

            // Register callbacks
            MovementAction.performed += ctx => MovementInput = ctx.ReadValue<Vector2>();
            MovementAction.canceled += ctx => MovementInput = Vector2.zero;

            InteractAction.performed += ctx => InteractTriggered = true;
            InteractAction.canceled += ctx => InteractTriggered = false;

            CastSpell1Action.performed += ctx => CastSpell1Triggered = true;
            CastSpell1Action.canceled += ctx => CastSpell1Triggered = false;

            CastSpell2Action.performed += ctx => CastSpell2Triggered = true;
            CastSpell2Action.canceled += ctx => CastSpell2Triggered = false;

            BuildAction.performed += ctx => BuildTriggered = true;
            BuildAction.canceled += ctx => BuildTriggered = false;

            JumpAction.performed += ctx => JumpTriggered = true;
            JumpAction.canceled += ctx => JumpTriggered = false;

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
            MovementAction.Disable();
            CastSpell1Action.Disable();
            CastSpell2Action.Disable();
            InteractAction.Disable();
            BuildAction.Disable();
            JumpAction.Disable();
        }

        private bool _isSimulated;

        private void Update()
        {
            // Self-heal: actions can end up disabled after domain reloads / prefab regeneration / script toggles.
            // This prevents the common "can't move" symptom from persisting silently.
            if (!_isSimulated && (MovementAction == null || !MovementAction.enabled))
            {
                EnsureActionsEnabled("Update self-heal");
            }

            if (!_isSimulated && MovementAction != null)
            {
                MovementInput = MovementAction.ReadValue<Vector2>();
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
    }
}
