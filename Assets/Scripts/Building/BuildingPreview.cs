using UnityEngine;
using Unity.Netcode;

namespace ByteWar.Building
{
    /// <summary>
    /// Manages the building preview ghost: instantiation, positioning, validity tinting, rotation, and cleanup.
    /// Extracted from BuildingController for single-responsibility.
    /// </summary>
    public class BuildingPreview
    {
        private GameObject _previewInstance;
        private float _previewYaw;
        private bool _positionValid;
        private Vector3 _lastValidPosition;
        private Quaternion _lastValidRotation;
        private MaterialPropertyBlock _propBlock;

        private static readonly int ColorPropId = Shader.PropertyToID("_Color");
        private static readonly Color ValidColor = new(0.2f, 0.9f, 0.2f, 0.45f);
        private static readonly Color InvalidColor = new(0.9f, 0.2f, 0.2f, 0.45f);

        public bool PositionValid => _positionValid;
        public Vector3 LastValidPosition => _lastValidPosition;
        public Quaternion LastValidRotation => _lastValidRotation;
        public float PreviewYaw => _previewYaw;
        public bool IsSnapped { get; set; }

        public BuildingPreview()
        {
            _propBlock = new MaterialPropertyBlock();
        }

        /// <summary>Rotates the preview yaw by one rotation step in the given direction.</summary>
        public void Rotate(float direction, float rotationStep)
        {
            _previewYaw = (_previewYaw + direction * rotationStep) % 360f;
            if (_previewYaw < 0f) _previewYaw += 360f;
            Debug.Log($"[BuildingPreview] Rotated to {_previewYaw:0} degrees");
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
                if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, placementMask))
                {
                    targetPos = hit.point;
                    _positionValid = true;
                }
            }

            Quaternion previewRot = Quaternion.Euler(0f, _previewYaw, 0f);

            // Grid snap
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

        /// <summary>Destroys the preview instance.</summary>
        public void Clear()
        {
            if (_previewInstance != null)
            {
                Object.Destroy(_previewInstance);
                _previewInstance = null;
            }
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
