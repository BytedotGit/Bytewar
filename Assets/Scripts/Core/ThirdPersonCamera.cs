using UnityEngine;
using UnityEngine.InputSystem;

namespace SurvivalRPG.Core
{
    /// <summary>
    /// World of Warcraft-style orbital third-person camera.
    /// Uses the new Input System API exclusively for full compatibility.
    ///
    /// Controls:
    ///   RMB drag      → orbit camera (character does NOT rotate)
    ///   LMB drag      → orbit camera (character rotates to match yaw on next move)
    ///   Both buttons  → move character forward
    ///   Scroll wheel  → zoom in / out
    ///   Camera-collision pushes the camera toward the player
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        // ── Public API ────────────────────────────────────────────────────────────
        public float CameraYaw => _yaw;
        public bool IsRightMouseHeld { get; private set; }
        public bool IsLeftMouseHeld { get; private set; }

        // ── Settings ──────────────────────────────────────────────────────────────
        [Header("Pivot")]
        [SerializeField] private float _pivotHeight = 1.4f;

        [Header("Orbit")]
        [SerializeField] private float _yaw = 0f;
        [SerializeField] private float _pitch = 22f;
        [SerializeField] private float _minPitch = -15f;
        [SerializeField] private float _maxPitch = 72f;
        [SerializeField] private float _mouseSensitivity = 0.15f;

        [Header("Zoom")]
        [SerializeField] private float _distance = 8f;
        [SerializeField] private float _minDistance = 0f;    // 0 = first-person (WoW)
        [SerializeField] private float _maxDistance = 30f;
        [SerializeField] private float _zoomSpeed = 0.8f;
        [SerializeField] private float _zoomSmoothing = 10f;
        private float _targetDistance;

        [Header("Smoothing")]
        [SerializeField] private float _orbitSmoothing = 14f;
        [SerializeField] private float _posSmoothing = 16f;

        [Header("Collision")]
        [SerializeField] private float _collisionRadius = 0.15f;
        [SerializeField] private LayerMask _collisionMask = ~0;

        // ── Private state ─────────────────────────────────────────────────────────
        private Transform _target;
        private float _smoothYaw;
        private float _smoothPitch;
        private bool _initialized;
        private Vector2 _lastMousePos;
        private Renderer[] _cachedRenderers;
        private bool _renderersVisible = true;
        private bool _cursorLockEnabled = true;

        private void Awake()
        {
            _targetDistance = _distance;
            _smoothYaw = _yaw;
            _smoothPitch = _pitch;

            // Exclude layer 2 (Ignore Raycast) from collision so we never hit the player
            _collisionMask &= ~(1 << 2);

            Debug.Log("[ThirdPersonCamera] Awake.");
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            _yaw = target.eulerAngles.y;
            _smoothYaw = _yaw;
            _initialized = true;

            // Cache renderers so we don't allocate every frame
            Transform playerRoot = target.parent != null ? target.parent : target;
            _cachedRenderers = playerRoot.GetComponentsInChildren<Renderer>();
            _renderersVisible = true;

            Debug.Log($"[ThirdPersonCamera] Target set to {target.name}. Cached {_cachedRenderers.Length} renderers.");
        }

        private void LateUpdate()
        {
            if (!_initialized || _target == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                _cursorLockEnabled = !_cursorLockEnabled;
                Debug.Log($"[ThirdPersonCamera] ESC pressed. cursorLockEnabled={_cursorLockEnabled}");
            }

            ReadMouseButtons(mouse);
            ReadOrbitInput(mouse);
            ReadZoomInput(mouse);
            ApplyCameraTransform();
        }

        private void ReadMouseButtons(Mouse mouse)
        {
            IsRightMouseHeld = mouse.rightButton.isPressed;
            IsLeftMouseHeld = mouse.leftButton.isPressed;
        }

        private void ReadOrbitInput(Mouse mouse)
        {
            // Allow either mouse button to orbit so trackpads / single-button mice still work.
            bool orbiting = (IsRightMouseHeld || IsLeftMouseHeld);

            if (orbiting)
            {
                // Locked cursor gives consistent mouse.delta in builds.
                if (_cursorLockEnabled && Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else if (!_cursorLockEnabled && Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }

                Vector2 mouseDelta = mouse.delta.ReadValue();
                _yaw += mouseDelta.x * _mouseSensitivity;
                _pitch -= mouseDelta.y * _mouseSensitivity;
                _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
            }
            else
            {
                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }

            _smoothYaw = Mathf.LerpAngle(_smoothYaw, _yaw, Time.deltaTime * _orbitSmoothing);
            _smoothPitch = Mathf.Lerp(_smoothPitch, _pitch, Time.deltaTime * _orbitSmoothing);
        }

        private void ReadZoomInput(Mouse mouse)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _targetDistance -= scroll * _zoomSpeed;
                _targetDistance = Mathf.Clamp(_targetDistance, _minDistance, _maxDistance);
            }
            _distance = Mathf.Lerp(_distance, _targetDistance, Time.deltaTime * _zoomSmoothing);
        }

        private void SetRenderersVisible(bool visible)
        {
            if (_renderersVisible == visible) return;
            _renderersVisible = visible;
            if (_cachedRenderers == null) return;
            foreach (var r in _cachedRenderers)
            {
                if (r != null) r.enabled = visible;
            }
        }

        private void ApplyCameraTransform()
        {
            Vector3 pivot = _target.position + Vector3.up * _pivotHeight;

            Quaternion rotation = Quaternion.Euler(_smoothPitch, _smoothYaw, 0f);
            Vector3 desiredDir = rotation * Vector3.back;

            float finalDist = _distance;
            if (_distance > 0.1f && Physics.SphereCast(pivot, _collisionRadius, desiredDir,
                out RaycastHit hit, _distance, _collisionMask, QueryTriggerInteraction.Ignore))
            {
                finalDist = Mathf.Max(hit.distance - 0.05f, 0f);
            }

            Vector3 targetPos = pivot + desiredDir * finalDist;
            transform.position = Vector3.Lerp(transform.position, targetPos,
                                              Time.deltaTime * _posSmoothing);
            transform.LookAt(pivot);

            // WoW-style: hide player model in first-person range
            bool firstPerson = finalDist < 0.5f;
            SetRenderersVisible(!firstPerson);
        }
    }
}
