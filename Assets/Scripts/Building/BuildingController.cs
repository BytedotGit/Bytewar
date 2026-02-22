using UnityEngine;
using Unity.Netcode;

namespace SurvivalRPG.Building
{
    public class BuildingController : NetworkBehaviour
    {
        public GameObject CurrentPreviewPrefab;
        public GameObject CurrentBuildingPrefab;
        public LayerMask PlacementLayerMask;
        public float MaxPlacementDistance = 10f;

        private GameObject _previewInstance;

        private void Update()
        {
            if (!IsSpawned || !IsOwner) return;

            if (CurrentPreviewPrefab != null)
            {
                UpdatePreviewPosition();

                if (Input.GetMouseButtonDown(0))
                {
                    PlaceBuildingServerRpc(_previewInstance.transform.position, _previewInstance.transform.rotation);
                }
            }
        }

        private void UpdatePreviewPosition()
        {
            if (_previewInstance == null)
            {
                _previewInstance = Instantiate(CurrentPreviewPrefab);
                // Disable colliders on preview
                foreach (var col in _previewInstance.GetComponentsInChildren<Collider>())
                {
                    col.enabled = false;
                }
            }

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, MaxPlacementDistance, PlacementLayerMask))
            {
                _previewInstance.transform.position = hit.point;
                // Basic snapping logic would go here
            }
        }

        [ServerRpc]
        private void PlaceBuildingServerRpc(Vector3 position, Quaternion rotation)
        {
            if (CurrentBuildingPrefab == null) return;

            GameObject building = Instantiate(CurrentBuildingPrefab, position, rotation);
            NetworkObject netObj = building.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
            }
        }

        public void SetBuildingPrefab(GameObject preview, GameObject actual)
        {
            CurrentPreviewPrefab = preview;
            CurrentBuildingPrefab = actual;
            if (_previewInstance != null)
            {
                Destroy(_previewInstance);
            }
        }
    }
}
