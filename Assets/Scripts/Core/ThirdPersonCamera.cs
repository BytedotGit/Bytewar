using UnityEngine;
using UnityEngine.InputSystem;

namespace ByteWar.Core
{
    /// <summary>
    /// World of Warcraft-style orbital third-person camera.
    /// Uses the new Input System API exclusively for full compatibility.
    ///
    /// Controls:
    ///   LMB drag      → orbit camera (does NOT affect character facing)
    ///   RMB drag      → orbit camera (character faces camera yaw while RMB is held; see PlayerMovement)
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
        public bool IsLeftMouseDragging { get; private set; }

        public float PivotHeight => _pivotHeight;
        public Transform Target => _target;
        public Vector3 PivotWorldPosition => (_target != null) ? (_target.position + Vector3.up * _pivotHeight) : transform.position;

        /// <summary>When true, scroll wheel zoom is suppressed (used by BuildingController).</summary>
        public bool SuppressZoom { get; set; }

        // ── Settings ──────────────────────────────────────────────────────────────
        [Header("Pivot")]
        [SerializeField] private float _pivotHeight = 1.4f;

        [Header("Orbit")]
        [SerializeField] private float _yaw = 0f;
        [SerializeField] private float _pitch = 22f;
        [SerializeField] private float _minPitch = -15f;
        [SerializeField] private float _maxPitch = 85f;
        [SerializeField] private float _mouseSensitivity = 0.15f;
        [Tooltip("Minimum cursor movement (in pixels) before LMB is treated as a camera orbit drag.")]
        [SerializeField] private float _leftMouseDragThresholdPx = 3f;

        [Header("Zoom")]
        [SerializeField] private float _distance = 8f;
        [SerializeField] private float _minDistance = 0f;    // 0 = first-person (WoW)
        [SerializeField] private float _maxDistance = 30f;
        [SerializeField] private float _zoomSpeed = 0.8f;
        [SerializeField] private float _zoomSmoothing = 10f;
        private float _targetDistance;

        [Header("First Person")]
        [Tooltip("When zoom distance is at or below this threshold, the camera enters first-person mode (player renderers hidden, collision disabled).")]
        [SerializeField] private float _firstPersonDistance = 0.5f;
        [Tooltip("Distance above which first-person mode exits (hysteresis to avoid flicker).")]
        [SerializeField] private float _firstPersonExitDistance = 0.65f;
        [Tooltip("Even in third-person, hide player renderers when the camera is very close to the pivot to avoid clipping.")]
        [SerializeField] private float _hideRenderersDistance = 0.5f;
        [Tooltip("Minimum allowed third-person distance (prevents LookAt singularity when collision pushes too close).")]
        [SerializeField] private float _minThirdPersonDistance = 0.05f;

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
        private Vector2 _leftMouseDragOrigin;
        private Renderer[] _cachedRenderers;
        private bool _renderersVisible = true;
        private bool _cursorLockEnabled = true;
        private bool _isFirstPerson;

        private const float FirstPersonMinPitch = -89.9f;
        private const float FirstPersonMaxPitch = 89.9f;

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
            SetTarget(target, _pivotHeight);
        }

        public void SetTarget(Transform target, float pivotHeight)
        {
            _target = target;
            _pivotHeight = pivotHeight;
            _yaw = target.eulerAngles.y;
            _smoothYaw = _yaw;
            _initialized = true;

            // Cache renderers so we don't allocate every frame
            Transform playerRoot = target.parent != null ? target.parent : target;
            _cachedRenderers = playerRoot.GetComponentsInChildren<Renderer>();
            _renderersVisible = true;

            Debug.Log($"[ThirdPersonCamera] Target set to {target.name}. pivotHeight={_pivotHeight:0.00}. Cached {_cachedRenderers.Length} renderers.");
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

            // LMB drag should orbit, but a simple click should not. Detect drag using mouse.position
            // while the cursor is still unlocked; once orbiting begins, we may lock the cursor.
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _leftMouseDragOrigin = mouse.position.ReadValue();
                IsLeftMouseDragging = false;
            }

            if (!IsLeftMouseHeld)
            {
                IsLeftMouseDragging = false;
                return;
            }

            if (!IsLeftMouseDragging)
            {
                Vector2 delta = mouse.position.ReadValue() - _leftMouseDragOrigin;
                float threshold = Mathf.Max(0.1f, _leftMouseDragThresholdPx);
                if (delta.sqrMagnitude >= threshold * threshold)
                    IsLeftMouseDragging = true;
            }
        }

        private void ReadOrbitInput(Mouse mouse)
        {
            // WoW-style: RMB orbits the camera (and drives facing). LMB drag orbits camera only.
            bool orbiting = IsRightMouseHeld || IsLeftMouseDragging;

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
                _pitch = ClampPitchForCurrentDistance(_pitch);
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

        private float ClampPitchForCurrentDistance(float pitch)
        {
            // When zoomed fully in (first-person), allow looking straight up/down.
            // Use the exit threshold so we keep a stable clamp across the hysteresis band.
            if (_distance <= _firstPersonExitDistance)
                return Mathf.Clamp(pitch, FirstPersonMinPitch, FirstPersonMaxPitch);

            return Mathf.Clamp(pitch, _minPitch, _maxPitch);
        }

        private void ReadZoomInput(Mouse mouse)
        {
            if (SuppressZoom) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _targetDistance -= scroll * _zoomSpeed;
                _targetDistance = Mathf.Clamp(_targetDistance, _minDistance, _maxDistance);
            }
            _distance = Mathf.Lerp(_distance, _targetDistance, Time.deltaTime * _zoomSmoothing);
        }

        internal void AutoTest_SetTargetDistance(float distance, bool immediate)
        {
            _targetDistance = Mathf.Clamp(distance, _minDistance, _maxDistance);
            if (immediate)
            {
                _distance = _targetDistance;
            }
        }

        internal void AutoTest_SetOrbitAngles(float yaw, float pitch, bool immediate)
        {
            _yaw = yaw;
            _pitch = pitch;
            if (immediate)
            {
                _smoothYaw = yaw;
                _smoothPitch = pitch;
            }
        }

        internal void AutoTest_SetMouseState(bool leftHeld, bool rightHeld, bool leftDragging)
        {
            IsLeftMouseHeld = leftHeld;
            IsRightMouseHeld = rightHeld;
            IsLeftMouseDragging = leftDragging;
        }

        internal void AutoTest_OrbitTick(Vector2 mouseDelta)
        {
            bool orbiting = IsRightMouseHeld || IsLeftMouseDragging;
            if (!orbiting)
                return;

            _yaw += mouseDelta.x * _mouseSensitivity;
            _pitch -= mouseDelta.y * _mouseSensitivity;
            _pitch = ClampPitchForCurrentDistance(_pitch);

            _smoothYaw = _yaw;
            _smoothPitch = _pitch;
        }

        internal void AutoTest_ClampPitchOnce()
        {
            _pitch = ClampPitchForCurrentDistance(_pitch);
            _smoothPitch = ClampPitchForCurrentDistance(_smoothPitch);
        }

        internal float AutoTest_GetPitch()
        {
            return _pitch;
        }

        internal void AutoTest_TickCameraTransformOnce()
        {
            if (!_initialized || _target == null) return;
            ApplyCameraTransform();
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

            // Determine (and log) first-person state using hysteresis.
            bool wantsFirstPerson = _distance <= _firstPersonDistance;
            bool nextFirstPerson = _isFirstPerson
                ? _distance <= _firstPersonExitDistance
                : wantsFirstPerson;

            if (nextFirstPerson != _isFirstPerson)
            {
                _isFirstPerson = nextFirstPerson;
                Debug.Log($"[ThirdPersonCamera] First-person changed: enabled={_isFirstPerson} distance={_distance:0.000} targetDistance={_targetDistance:0.000}");
            }

            if (_isFirstPerson)
            {
                // True first-person: place camera at pivot and use yaw/pitch rotation directly.
                // Collision disabled in first-person (stable; avoids jitter).
                transform.position = pivot;
                transform.rotation = rotation;
                SetRenderersVisible(false);
                return;
            }

            Vector3 desiredDir = rotation * Vector3.back;

            float finalDist = Mathf.Max(_distance, _minThirdPersonDistance);
            if (finalDist > _minThirdPersonDistance && Physics.SphereCast(pivot, _collisionRadius, desiredDir,
                    out RaycastHit hit, finalDist, _collisionMask, QueryTriggerInteraction.Ignore))
            {
                finalDist = Mathf.Max(hit.distance - 0.05f, _minThirdPersonDistance);
            }

            Vector3 targetPos = pivot + desiredDir * finalDist;
            transform.position = Vector3.Lerp(transform.position, targetPos,
                                              Time.deltaTime * _posSmoothing);
            transform.LookAt(pivot);

            // Hide player when very close to avoid clipping.
            bool hidePlayer = finalDist < _hideRenderersDistance;
            SetRenderersVisible(!hidePlayer);
        }
    }
}
