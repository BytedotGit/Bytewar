using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using ByteWar.Survival;
using ByteWar.Core;
using System;
using System.Collections.Generic;

namespace ByteWar.Building
{
    /// <summary>
    /// Valheim-like build mode controller.
    /// Toggle with B key. Scroll wheel rotates piece. Number keys (1-9) select recipes.
    /// Left-click places. Middle-click removes. Right-click repairs.
    /// Supports edge-to-edge adjacency snapping. Server-authoritative cost + placement.
    /// Preview rendering delegated to <see cref="BuildingPreview"/>.
    /// Snap/support logic delegated to <see cref="BuildingSnap"/>.
    /// </summary>
    public class BuildingController : NetworkBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private List<BuildingRecipe> _recipes = new();
        [SerializeField] private LayerMask _placementLayerMask = ~0;
        [SerializeField] private float _maxPlacementDistance = 20f;
        [SerializeField] private float _gridSize = 4f;
        [SerializeField] private float _adjacencySnapRadius = 3f;
        [SerializeField] private float _rotationStep = 45f;
        [SerializeField] private float _removeCooldown = 0.3f;

        [Header("Prefab Registry (set by PrefabGenerator)")]
        [SerializeField] private GameObject _foundationPrefab;
        [SerializeField] private GameObject _wallPrefab;

        // Runtime state
        private bool _buildModeActive;
        private int _selectedRecipeIndex;
        private float _lastRemoveTime;

        // Non-alloc overlap
        private readonly Collider[] _overlapBuffer = new Collider[32];

        private PlayerInputHandler _inputHandler;
        private InventoryComponent _inventory;
        private ThirdPersonCamera _cameraController;
        private BuildingPreview _preview;

        /// <summary>Raised on the owner when build mode is toggled. True = entered.</summary>
        public event Action<bool> OnBuildModeChanged;

        /// <summary>Raised on the server after a building piece is successfully spawned.</summary>
        public event Action<BuildingPieceType, Vector3> OnBuildingPlaced;

        public bool IsBuildModeActive => _buildModeActive;
        public int SelectedRecipeIndex => _selectedRecipeIndex;
        public List<BuildingRecipe> Recipes => _recipes;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _inputHandler = GetComponent<PlayerInputHandler>();
            _inventory = GetComponent<InventoryComponent>();
            _preview = new BuildingPreview();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                Camera cam = Camera.main;
                if (cam != null) _cameraController = cam.GetComponent<ThirdPersonCamera>();
            }
        }

        public override void OnNetworkDespawn()
        {
            _preview.Clear();
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner) return;

            // Toggle build mode
            if (_inputHandler != null && _inputHandler.ConsumeBuild())
            {
                ToggleBuildMode();
            }

            if (!_buildModeActive) return;

            HandleRecipeSelection();
            HandleRotation();
            UpdatePreview();
            HandlePlacement();
            HandleRemoveRepair();
        }

        // ── Build mode ────────────────────────────────────────────────────────────

        public void ToggleBuildMode()
        {
            _buildModeActive = !_buildModeActive;
            Debug.Log($"[BuildingController] Build mode {(_buildModeActive ? "ON" : "OFF")} owner={OwnerClientId}");

            if (_cameraController != null)
                _cameraController.SuppressZoom = _buildModeActive;

            if (!_buildModeActive)
            {
                _preview.Clear();
            }

            OnBuildModeChanged?.Invoke(_buildModeActive);
        }

        // ── Recipe selection (number keys 1-9) ───────────────────────────────────

        private void HandleRecipeSelection()
        {
            if (_recipes.Count == 0) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            // Number keys select recipe directly
            for (int i = 0; i < Mathf.Min(_recipes.Count, 9); i++)
            {
                if (GetNumberKeyPressed(kb, i + 1))
                {
                    if (_selectedRecipeIndex != i)
                    {
                        _selectedRecipeIndex = i;
                        Debug.Log($"[BuildingController] Selected recipe [{i + 1}]: {_recipes[i].RecipeName}");
                        _preview.Clear();
                    }
                    break;
                }
            }
        }

        private static bool GetNumberKeyPressed(Keyboard kb, int number)
        {
            return number switch
            {
                1 => kb.digit1Key.wasPressedThisFrame,
                2 => kb.digit2Key.wasPressedThisFrame,
                3 => kb.digit3Key.wasPressedThisFrame,
                4 => kb.digit4Key.wasPressedThisFrame,
                5 => kb.digit5Key.wasPressedThisFrame,
                6 => kb.digit6Key.wasPressedThisFrame,
                7 => kb.digit7Key.wasPressedThisFrame,
                8 => kb.digit8Key.wasPressedThisFrame,
                9 => kb.digit9Key.wasPressedThisFrame,
                _ => false,
            };
        }

        // ── Rotation (scroll wheel) ──────────────────────────────────────────────

        private void HandleRotation()
        {
            if (Mouse.current == null) return;
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.1f)
            {
                float direction = scroll > 0f ? 1f : -1f;
                _preview.Rotate(direction, _rotationStep);
            }
        }

        // ── Preview ───────────────────────────────────────────────────────────────

        private void UpdatePreview()
        {
            if (_recipes.Count == 0) return;

            BuildingRecipe recipe = _recipes[_selectedRecipeIndex];
            GameObject prefab = GetPrefabForType(recipe.PieceType);
            if (prefab == null) return;

            _preview.UpdatePreview(prefab, transform, Camera.main,
                _maxPlacementDistance, _placementLayerMask, recipe.CanAfford(_inventory), true);

            // Get pending position/rotation from preview and apply adjacency snap
            Vector3 pos = _preview.LastValidPosition;
            Quaternion rot = _preview.LastValidRotation;
            bool snapped = BuildingSnap.TryAdjacencySnap(
                ref pos, ref rot, recipe.PieceType,
                _adjacencySnapRadius, _gridSize, _overlapBuffer);
            _preview.IsSnapped = snapped;
            _preview.ApplySnappedTransform(pos, rot);

            // Validity: can afford + position valid + support check
            bool canAfford = recipe.CanAfford(_inventory);
            bool supported = BuildingSnap.CheckSupport(pos, recipe.PieceType, snapped, _gridSize, _overlapBuffer);
            bool valid = _preview.PositionValid && canAfford && supported;
            _preview.FinalizePreview(valid);
        }

        // ── Placement ─────────────────────────────────────────────────────────────

        private void HandlePlacement()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            if (!_preview.PositionValid || _recipes.Count == 0) return;

            BuildingRecipe recipe = _recipes[_selectedRecipeIndex];
            if (!recipe.CanAfford(_inventory))
            {
                Debug.Log($"[BuildingController] Cannot afford '{recipe.RecipeName}'.");
                return;
            }

            if (!BuildingSnap.CheckSupport(_preview.LastValidPosition, recipe.PieceType,
                    _preview.IsSnapped, _gridSize, _overlapBuffer))
            {
                Debug.Log($"[BuildingController] Placement denied: no structural support.");
                return;
            }

            PlaceBuildingServerRpc(_preview.LastValidPosition, _preview.LastValidRotation, _selectedRecipeIndex);
        }

        [ServerRpc]
        private void PlaceBuildingServerRpc(Vector3 position, Quaternion rotation, int recipeIndex)
        {
            if (recipeIndex < 0 || recipeIndex >= _recipes.Count)
            {
                Debug.LogWarning($"[BuildingController] Invalid recipe index {recipeIndex} from client {OwnerClientId}.");
                return;
            }

            BuildingRecipe recipe = _recipes[recipeIndex];

            // Server-authoritative cost validation
            InventoryComponent serverInventory = GetComponent<InventoryComponent>();
            if (serverInventory == null || !recipe.ConsumeResources(serverInventory))
            {
                Debug.Log($"[BuildingController] Server rejected placement: insufficient resources for '{recipe.RecipeName}' from client {OwnerClientId}.");
                return;
            }

            GameObject prefab = GetPrefabForType(recipe.PieceType);
            if (prefab == null)
            {
                Debug.LogError($"[BuildingController] No prefab registered for {recipe.PieceType}!");
                return;
            }

            GameObject building = Instantiate(prefab, position, rotation);
            var piece = building.GetComponent<BuildingPiece>();
            if (piece != null)
            {
                piece.PlacedByClientId.Value = OwnerClientId;
            }

            NetworkObject netObj = building.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                Debug.Log($"[BuildingController] Placed {recipe.PieceType} at {position} rot={rotation.eulerAngles} by client {OwnerClientId} netObj={netObj.NetworkObjectId}");
            }
            else
            {
                Debug.LogError($"[BuildingController] Spawned building missing NetworkObject! Destroying.");
                Destroy(building);
                return;
            }

            OnBuildingPlaced?.Invoke(recipe.PieceType, position);
        }

        // ── Remove / Repair ───────────────────────────────────────────────────────

        private void HandleRemoveRepair()
        {
            if (Mouse.current == null) return;

            // Middle-click: remove
            if (Mouse.current.middleButton.wasPressedThisFrame && Time.time - _lastRemoveTime > _removeCooldown)
            {
                if (TryGetTargetPiece(out BuildingPiece target))
                {
                    _lastRemoveTime = Time.time;
                    RemoveBuildingServerRpc(target.NetworkObjectId);
                }
            }

            // Right-click in build mode: repair
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (TryGetTargetPiece(out BuildingPiece target))
                {
                    RepairBuildingServerRpc(target.NetworkObjectId);
                }
            }
        }

        private bool TryGetTargetPiece(out BuildingPiece piece)
        {
            piece = null;
            Camera cam = Camera.main;
            if (cam == null || Mouse.current == null) return false;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0));
            if (Physics.Raycast(ray, out RaycastHit hit, _maxPlacementDistance))
            {
                piece = hit.collider.GetComponentInParent<BuildingPiece>();
                return piece != null;
            }
            return false;
        }

        [ServerRpc]
        private void RemoveBuildingServerRpc(ulong networkObjectId)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
            {
                Debug.LogWarning($"[BuildingController] Remove: netObj {networkObjectId} not found.");
                return;
            }

            var piece = netObj.GetComponent<BuildingPiece>();
            if (piece == null)
            {
                Debug.LogWarning($"[BuildingController] Remove: object {networkObjectId} is not a BuildingPiece.");
                return;
            }

            // Refund resources to the owner
            int recipeIdx = FindRecipeForType(piece.PieceType);
            if (recipeIdx >= 0)
            {
                InventoryComponent inv = GetComponent<InventoryComponent>();
                if (inv != null)
                {
                    foreach (var ingredient in _recipes[recipeIdx].Cost)
                    {
                        for (int j = 0; j < ingredient.Amount; j++)
                            inv.AddItem(ingredient.Item);
                    }
                    Debug.Log($"[BuildingController] Refunded resources for {piece.PieceType} to client {OwnerClientId}.");
                }
            }

            Debug.Log($"[BuildingController] Removed {piece.PieceType} netObj={networkObjectId} by client {OwnerClientId}.");
            netObj.Despawn(true);
        }

        [ServerRpc]
        private void RepairBuildingServerRpc(ulong networkObjectId)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
            {
                Debug.LogWarning($"[BuildingController] Repair: netObj {networkObjectId} not found.");
                return;
            }

            var piece = netObj.GetComponent<BuildingPiece>();
            if (piece == null)
            {
                Debug.LogWarning($"[BuildingController] Repair: object {networkObjectId} is not a BuildingPiece.");
                return;
            }

            piece.Repair();
            Debug.Log($"[BuildingController] Repaired {piece.PieceType} netObj={networkObjectId} by client {OwnerClientId}.");
        }

        private int FindRecipeForType(BuildingPieceType type)
        {
            for (int i = 0; i < _recipes.Count; i++)
            {
                if (_recipes[i].PieceType == type) return i;
            }
            return -1;
        }

        // ── Prefab lookup ─────────────────────────────────────────────────────────

        private GameObject GetPrefabForType(BuildingPieceType type)
        {
            return type switch
            {
                BuildingPieceType.Foundation => _foundationPrefab,
                BuildingPieceType.Wall => _wallPrefab,
                _ => null,
            };
        }

        /// <summary>Set building prefabs at generation time (called by PrefabGenerator).</summary>
        public void SetBuildingPrefabs(GameObject foundation, GameObject wall)
        {
            _foundationPrefab = foundation;
            _wallPrefab = wall;
        }

        /// <summary>Set recipes at generation time.</summary>
        public void SetRecipes(List<BuildingRecipe> recipes)
        {
            _recipes = recipes ?? new List<BuildingRecipe>();
        }
    }
}
