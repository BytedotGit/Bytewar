using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace ByteWar.Building
{
    /// <summary>
    /// Manages the building preview ghost: instantiation, positioning, validity tinting, rotation, and cleanup.
    /// Caches <see cref="SnapPointMarker"/> local positions from the prefab for nearest-pair snap detection.
    /// </summary>
    public class BuildingPreview
    {
        private GameObject _previewInstance;
        private float _previewYaw;
        private float _previewPitch;
        private bool _positionValid;
        private Vector3 _lastValidPosition;
        private Vector3 _lastRawPosition;
        private Quaternion _lastValidRotation;
        private MaterialPropertyBlock _propBlock;

        /// <summary>Cached local-space snap point positions from the current preview prefab.</summary>
        private readonly List<Vector3> _snapPointLocals = new();
        /// <summary>Cached snap point types aligned to <see cref="_snapPointLocals"/>.</summary>
        private readonly List<SnapPointType> _snapPointTypes = new();

        private static readonly int ColorPropId = Shader.PropertyToID("_Color");
        private static readonly Color ValidColor = new(0.2f, 0.9f, 0.2f, 0.45f);
        private static readonly Color InvalidColor = new(0.9f, 0.2f, 0.2f, 0.45f);

        /// <summary>Maximum pitch angle in either direction (degrees).</summary>
        public const float MaxPitch = 90f;

        public bool PositionValid => _positionValid;
        public Vector3 LastValidPosition => _lastValidPosition;
        /// <summary>Pre-grid-snap raycast position for adjacency snap detection.</summary>
        public Vector3 LastRawPosition => _lastRawPosition;
        public Quaternion LastValidRotation => _lastValidRotation;
        public float PreviewYaw => _previewYaw;
        public float PreviewPitch => _previewPitch;
        public bool IsSnapped { get; set; }

        /// <summary>Local-space snap point positions for the current preview piece.</summary>
        public List<Vector3> SnapPointLocals => _snapPointLocals;
        public List<SnapPointType> SnapPointTypes => _snapPointTypes;

        /// <summary>Hit surface angle from last raycast (degrees from vertical).</summary>
        public float LastSurfaceAngle { get; private set; }

        /// <summary>The building piece directly hit by the placement raycast (if any).</summary>
        public BuildingPiece LastHitPiece { get; private set; }

        /// <summary>Raycast hit point from the last placement raycast.</summary>
        public Vector3 LastHitPoint { get; private set; }

        /// <summary>Aim ray used for the last placement raycast.</summary>
        public Ray LastAimRay { get; private set; }

        /// <summary>Half-extents of the preview piece bounding box (for overlap checks).</summary>
        public Vector3 HalfExtents { get; private set; }

        public BuildingPreview()
        {
            _propBlock = new MaterialPropertyBlock();
        }

        /// <summary>Rotates the preview yaw by one rotation step in the given direction.</summary>
        public void Rotate(float direction, float rotationStep)
        {
            _previewYaw = (_previewYaw + direction * rotationStep) % 360f;
            if (_previewYaw < 0f) _previewYaw += 360f;
            Debug.Log($"[BuildingPreview] Rotated yaw to {_previewYaw:0} degrees");
        }

        /// <summary>Rotates the preview pitch by one step in the given direction, clamped to ±MaxPitch.</summary>
        public void RotatePitch(float direction, float rotationStep)
        {
            float raw = _previewPitch + direction * rotationStep;
            // Snap to nearest multiple of rotationStep so 0° and 90° are always reachable.
            raw = Mathf.Round(raw / rotationStep) * rotationStep;
            _previewPitch = Mathf.Clamp(raw, -MaxPitch, MaxPitch);
            Debug.Log($"[BuildingPreview] Rotated pitch to {_previewPitch:0} degrees");
        }

        /// <summary>Resets pitch to zero.</summary>
        public void ResetPitch()
        {
            _previewPitch = 0f;
        }

        /// <summary>
        /// Ensures the preview instance exists and updates its position, rotation, and tint.
        /// Returns the preview Quaternion before adjacency snapping (based on yaw only).
        /// </summary>
        public void UpdatePreview(
            GameObject prefab,
            Transform owner,
            Camera cam,
            float maxDistance,
            LayerMask placementMask,
            bool canAfford,
            bool isSupported)
        {
            EnsurePreviewInstance(prefab);

            // Raycast for placement position
            Vector3 targetPos = owner.position + owner.forward * 5f;
            _positionValid = false;

            if (cam != null && UnityEngine.InputSystem.Mouse.current != null)
            {
                Vector2 screenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                Ray ray = cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0));
                LastAimRay = ray;
                if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, placementMask))
                {
                    targetPos = hit.point;
                    _positionValid = true;
                    // Track surface angle for placement restrictions
                    LastSurfaceAngle = Vector3.Angle(hit.normal, Vector3.up);

                    LastHitPoint = hit.point;
                    LastHitPiece = hit.collider != null ? hit.collider.GetComponentInParent<BuildingPiece>() : null;
                }
                else
                {
                    // Valheim-style: when aiming into empty space, the ghost still follows the aim ray.
                    // Placement validity will be enforced by snapping/support checks in BuildingController.
                    targetPos = ray.origin + ray.direction * maxDistance;
                    LastSurfaceAngle = 0f;
                    LastHitPoint = targetPos;
                    LastHitPiece = null;
                }
            }

            Quaternion previewRot = Quaternion.Euler(_previewPitch, _previewYaw, 0f);

            // Store raw position for adjacency snap (before grid snap)
            _lastRawPosition = targetPos;

            // Grid snap (fallback when adjacency snap misses)
            targetPos = BuildingSnap.SnapToGrid(targetPos, 4f);

            _lastValidPosition = targetPos;
            _lastValidRotation = previewRot;
        }

        /// <summary>Applies snapped position/rotation after adjacency check.</summary>
        public void ApplySnappedTransform(Vector3 position, Quaternion rotation)
        {
            _lastValidPosition = position;
            _lastValidRotation = rotation;

            if (_previewInstance != null)
            {
                _previewInstance.transform.position = position;
                _previewInstance.transform.rotation = rotation;
            }
        }

        /// <summary>Finalizes preview positioning and sets tint color.</summary>
        public void FinalizePreview(bool valid)
        {
            if (_previewInstance == null) return;
            _previewInstance.transform.position = _lastValidPosition;
            _previewInstance.transform.rotation = _lastValidRotation;
            SetColor(valid ? ValidColor : InvalidColor);
        }

        /// <summary>Destroys the preview instance and clears cached snap points.</summary>
        public void Clear()
        {
            if (_previewInstance != null)
            {
                Object.Destroy(_previewInstance);
                _previewInstance = null;
            }
            _snapPointLocals.Clear();
            _snapPointTypes.Clear();

            LastHitPiece = null;
            LastHitPoint = default;
            LastAimRay = default;
        }

        private void EnsurePreviewInstance(GameObject prefab)
        {
            if (_previewInstance != null || prefab == null) return;

            _previewInstance = Object.Instantiate(prefab);
            _previewInstance.name = "BuildingPreview";

            // Strip networking and physics from preview
            foreach (var nb in _previewInstance.GetComponentsInChildren<NetworkBehaviour>())
                Object.Destroy(nb);
            var no = _previewInstance.GetComponent<NetworkObject>();
            if (no != null) Object.Destroy(no);
            foreach (var col in _previewInstance.GetComponentsInChildren<Collider>())
                col.enabled = false;
            foreach (var rb in _previewInstance.GetComponentsInChildren<Rigidbody>())
                Object.Destroy(rb);

            // Cache snap point local positions from the prefab
            _snapPointLocals.Clear();
            _snapPointTypes.Clear();
            var markers = prefab.GetComponentsInChildren<SnapPointMarker>();
            foreach (var m in markers)
            {
                _snapPointLocals.Add(m.transform.localPosition);
                _snapPointTypes.Add(m.SnapType);
            }

            // Cache half-extents from the prefab's colliders for overlap checking
            var prefabColliders = prefab.GetComponentsInChildren<Collider>();
            Bounds combinedBounds = new(prefab.transform.position, Vector3.zero);
            foreach (var c in prefabColliders)
                combinedBounds.Encapsulate(c.bounds);
            HalfExtents = combinedBounds.extents;

            Debug.Log($"[BuildingPreview] Created preview with {_snapPointLocals.Count} snap points, halfExtents={HalfExtents}");
        }

        private void SetColor(Color color)
        {
            if (_previewInstance == null) return;
            _propBlock.SetColor(ColorPropId, color);
            foreach (var r in _previewInstance.GetComponentsInChildren<Renderer>())
            {
                r.SetPropertyBlock(_propBlock);
            }
        }
    }
}
