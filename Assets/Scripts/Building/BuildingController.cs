using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using ByteWar.Survival;
using ByteWar.Core;
using ByteWar.UI;
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
        private const string DeveloperPlacementCatalogResourcesPath = "Generated/DeployableAssetCatalog";
        private const string DeveloperPlacementFallbackResourcesPath = "Generated/BlenderE2EProp/BlenderE2EProp";

        [Header("Configuration")]
        [SerializeField] private List<BuildingRecipe> _recipes = new();
        [SerializeField] private LayerMask _placementLayerMask = ~0;
        [SerializeField] private float _maxPlacementDistance = 20f;
        [SerializeField] private float _gridSize = 4f;
        [SerializeField] private float _adjacencySnapRadius = 5f;
        [SerializeField] private float _rotationStep = 22.5f;
        [SerializeField] private float _removeCooldown = 0.3f;
        [SerializeField] private bool _internalBuildToggleInputEnabled = false;

        [Header("Prefab Registry (set by PrefabGenerator)")]
        [SerializeField] private GameObject _foundationPrefab;
        [SerializeField] private GameObject _wallPrefab;
        [SerializeField] private GameObject _floorPrefab;
        [SerializeField] private GameObject _rampPrefab;
        [SerializeField] private GameObject _roof26Prefab;
        [SerializeField] private GameObject _stairsPrefab;
        [SerializeField] private GameObject _polePrefab;
        [SerializeField] private GameObject _beamPrefab;
        [SerializeField] private GameObject _angledWallPrefab;
        [SerializeField] private GameObject _doorFramePrefab;
        [SerializeField] private GameObject _windowPrefab;
        [SerializeField] private GameObject _halfWallPrefab;

        /// <summary>All building piece prefabs for inspection (e.g. AutoTester).</summary>
        public GameObject[] BuildingPrefabs => new[]
        {
            _foundationPrefab, _wallPrefab, _floorPrefab, _rampPrefab,
            _roof26Prefab, _stairsPrefab, _polePrefab, _beamPrefab, _angledWallPrefab,
            _doorFramePrefab, _windowPrefab, _halfWallPrefab
        };

        // Runtime state
        private bool _buildModeActive;
        private bool _externalInputSuppressed;
        private bool _externalRightClickCancelMode;
        private bool _externalCancelOnRightClick = true;
        private int _selectedRecipeIndex;
        private float _lastRemoveTime;

        // Non-alloc overlap
        private readonly Collider[] _overlapBuffer = new Collider[32];

        private static readonly HashSet<string> _developerPlacementAllowlist = new(StringComparer.OrdinalIgnoreCase);
        private static bool _developerPlacementAllowlistInitialized;

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
        public bool IsExternalInputSuppressed => _externalInputSuppressed;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _inputHandler = GetComponent<PlayerInputHandler>();
            _inventory = GetComponent<InventoryComponent>();
            _preview = new BuildingPreview();
            _internalBuildToggleInputEnabled = false;
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
            if (_internalBuildToggleInputEnabled && _inputHandler != null && _inputHandler.ConsumeBuild())
            {
                ToggleBuildMode();
            }

            if (!_buildModeActive) return;

            if (_externalInputSuppressed)
                return;

            if (ShouldCancelBuildModeThisFrame())
            {
                SetBuildModeActive(false);
                return;
            }

            HandleRecipeSelection();
            HandleRotation();
            UpdatePreview();
            HandlePlacement();
            HandleRemoveRepair();
        }

        // ── Build mode ────────────────────────────────────────────────────────────

        public void ToggleBuildMode()
        {
            SetBuildModeActive(!_buildModeActive);
        }

        public void SetInternalBuildToggleInputEnabled(bool enabled)
        {
            _internalBuildToggleInputEnabled = enabled;
        }

        public void SetBuildModeActive(bool active, bool clearPreview = true)
        {
            if (_buildModeActive == active)
            {
                if (clearPreview)
                    _preview.Clear();

                if (_cameraController != null)
                    _cameraController.SuppressZoom = _buildModeActive;
                return;
            }

            _buildModeActive = active;
            Debug.Log($"[BuildingController] Build mode {(_buildModeActive ? "ON" : "OFF")} owner={OwnerClientId}");

            if (_cameraController != null)
                _cameraController.SuppressZoom = _buildModeActive;

            if (clearPreview)
                _preview.Clear();

            OnBuildModeChanged?.Invoke(_buildModeActive);
        }

        public bool TrySelectRecipeIndex(int recipeIndex, bool clearPreview = true)
        {
            if (recipeIndex < 0 || recipeIndex >= _recipes.Count)
                return false;

            if (_selectedRecipeIndex != recipeIndex)
            {
                _selectedRecipeIndex = recipeIndex;
                Debug.Log($"[BuildingController] Selected recipe [{recipeIndex + 1}]: {_recipes[recipeIndex].RecipeName}");
            }

            if (clearPreview)
                ClearPreview();

            return true;
        }

        public bool TrySelectRecipeByType(BuildingPieceType pieceType, bool clearPreview = true)
        {
            int recipeIndex = FindRecipeForType(pieceType);
            if (recipeIndex < 0)
                return false;

            return TrySelectRecipeIndex(recipeIndex, clearPreview);
        }

        public void SetExternalRightClickCancelMode(bool enabled)
        {
            _externalRightClickCancelMode = enabled;
            if (!enabled)
                _externalCancelOnRightClick = true;
        }

        public void SetExternalCancelOnRightClick(bool enabled)
        {
            _externalCancelOnRightClick = enabled;
        }

        public bool TryRequestDeveloperAssetPlacement(Vector3 position, Quaternion rotation, string resourcePath, string displayName)
        {
            if (!IsSpawned || !IsOwner)
            {
                Debug.LogWarning("[BuildingController] Developer asset placement denied: requester is not the spawned owner.");
                return false;
            }

            if (!TryNormalizeResourcePath(resourcePath, out string normalizedPath))
            {
                Debug.LogWarning($"[BuildingController] Developer asset placement denied: invalid resource path '{resourcePath}'.");
                return false;
            }

            PlaceDeveloperAssetServerRpc(position, rotation, normalizedPath, displayName ?? string.Empty);
            return true;
        }

        public void SetExternalInputSuppressed(bool suppressed)
        {
            _externalInputSuppressed = suppressed;
            if (_externalInputSuppressed)
                _preview.Clear();
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
                    TrySelectRecipeIndex(i);
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

        // ── Rotation (scroll wheel / Shift+scroll for pitch) ──────────────────

        private void HandleRotation()
        {
            if (Mouse.current == null) return;
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.1f)
            {
                float direction = scroll > 0f ? 1f : -1f;
                bool shiftHeld = Keyboard.current != null &&
                    (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
                if (shiftHeld)
                    _preview.RotatePitch(direction, _rotationStep);
                else
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

            // Valheim-style nearest-pair snap using SnapPointMarker children
            Vector3 rawPos = _preview.LastRawPosition;
            Quaternion rot = _preview.LastValidRotation;
            bool snapped = BuildingSnap.TryAdjacencySnap(
                ref rawPos,
                rot,
                _preview.SnapPointLocals,
                _preview.SnapPointTypes,
                _adjacencySnapRadius,
                _overlapBuffer,
                _preview.LastHitPiece);
            _preview.IsSnapped = snapped;

            // Use snapped position if found; otherwise free placement with surface normal
            Vector3 pos;
            if (snapped)
            {
                pos = rawPos;
            }
            else
            {
                // Free placement: use raw raycast position (no grid snap) for natural feel
                pos = _preview.LastRawPosition;
            }
            _preview.ApplySnappedTransform(pos, rot);

            // Validity: can afford + position valid + support check + overlap + restrictions
            bool canAfford = recipe.CanAfford(_inventory);
            bool supported = BuildingSnap.CheckSupport(pos, recipe.PieceType, snapped, _gridSize, _overlapBuffer);
            bool noOverlap = BuildingSnap.CheckNoOverlap(pos, rot, _preview.HalfExtents, _overlapBuffer);
            bool positionOk = _preview.PositionValid || snapped;
            bool valid = positionOk && canAfford && supported && noOverlap;
            _preview.FinalizePreview(valid);
        }

        private void ClearPreview()
        {
            _preview.Clear();
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

            // Broadcast VFX + audio to all clients
            PlayBuildingEffectClientRpc(position);

            // Raise event bus channel
            GameEventBus.BuildingPlaced.Raise(new BuildingPlacedEvent
            {
                PieceType = (int)recipe.PieceType,
                Position = position
            });
        }

        [ClientRpc]
        private void PlayBuildingEffectClientRpc(Vector3 position)
        {
            Debug.Log($"[BuildingController] PlayBuildingEffectClientRpc at {position}");
            if (Core.VFXManager.Instance != null)
                Core.VFXManager.Instance.PlayEffectLocal(Core.VFXType.BuildingPlace, position);
            if (Core.AudioManager.Instance != null)
                Core.AudioManager.Instance.PlaySFX(Core.SFXType.BuildingPlace, position);
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
            if (_externalRightClickCancelMode)
            {
                return;
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (TryGetTargetPiece(out BuildingPiece target))
                {
                    RepairBuildingServerRpc(target.NetworkObjectId);
                }
            }
        }

        private bool ShouldCancelBuildModeThisFrame()
        {
            if (!_externalRightClickCancelMode)
                return false;

            bool rightClickPressed = _externalCancelOnRightClick && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
            bool escapePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            return rightClickPressed || escapePressed;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void PlaceDeveloperAssetServerRpc(Vector3 position, Quaternion rotation, string resourcePath, string displayName, RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (senderClientId != OwnerClientId)
            {
                Debug.LogWarning($"[BuildingController] Developer asset placement denied: sender {senderClientId} is not owner {OwnerClientId}.");
                return;
            }

            if (!CanBypassBuildingCostsForDeveloperPlacement())
            {
                Debug.LogWarning($"[BuildingController] Developer asset placement denied for client {senderClientId}: DevMode/bypass not enabled.");
                return;
            }

            if (!TryNormalizeResourcePath(resourcePath, out string normalizedPath))
            {
                Debug.LogWarning($"[BuildingController] Developer asset placement denied: invalid resource path '{resourcePath}'.");
                return;
            }

            if (!IsDeveloperPlacementPathAllowlisted(normalizedPath))
            {
                Debug.LogWarning($"[BuildingController] Developer asset placement denied: resource path '{normalizedPath}' is not allowlisted.");
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(normalizedPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[BuildingController] Developer asset placement denied: prefab not found at Resources path '{normalizedPath}'.");
                return;
            }

            var prefabNetworkObject = prefab.GetComponent<NetworkObject>();
            var prefabCollider = prefab.GetComponentInChildren<Collider>(true);
            var prefabLodGroup = prefab.GetComponentInChildren<LODGroup>(true);
            if (prefabNetworkObject == null || prefabCollider == null || prefabLodGroup == null)
            {
                Debug.LogWarning($"[BuildingController] Developer asset placement denied for '{normalizedPath}': required components missing (NetworkObject={prefabNetworkObject != null}, Collider={prefabCollider != null}, LODGroup={prefabLodGroup != null}).");
                return;
            }

            GameObject instance = Instantiate(prefab, position, rotation);
            string resolvedName = string.IsNullOrWhiteSpace(displayName) ? prefab.name : displayName.Trim();
            instance.name = resolvedName;

            NetworkObject spawnedNetworkObject = instance.GetComponent<NetworkObject>();
            if (spawnedNetworkObject == null)
            {
                Debug.LogError("[BuildingController] Developer asset instance missing NetworkObject after instantiate. Destroying instance.");
                Destroy(instance);
                return;
            }

            spawnedNetworkObject.Spawn();
            Debug.Log($"[BuildingController] Developer asset placed '{resolvedName}' from '{normalizedPath}' at {position} by client {senderClientId} netObj={spawnedNetworkObject.NetworkObjectId}");

            PlayBuildingEffectClientRpc(position);
        }

        private static bool CanBypassBuildingCostsForDeveloperPlacement()
        {
            return GameConstants.ShouldBypassBuildingCosts() || GameConstants.IsDevMode();
        }

        private static bool IsDeveloperPlacementPathAllowlisted(string normalizedPath)
        {
            EnsureDeveloperPlacementAllowlistInitialized();
            return IsDeveloperPlacementPathAllowlisted(normalizedPath, _developerPlacementAllowlist);
        }

        private static void EnsureDeveloperPlacementAllowlistInitialized()
        {
            if (_developerPlacementAllowlistInitialized)
                return;

            _developerPlacementAllowlistInitialized = true;
            _developerPlacementAllowlist.Clear();

            if (TryNormalizeResourcePath(DeveloperPlacementFallbackResourcesPath, out string fallbackPath))
                _developerPlacementAllowlist.Add(fallbackPath);

            var catalog = Resources.Load<DeployableAssetCatalog>(DeveloperPlacementCatalogResourcesPath);
            if (catalog == null || catalog.Entries == null)
            {
                Debug.LogWarning("[BuildingController] Developer placement allowlist: catalog missing or empty; using fallback allowlist entries only.");
                return;
            }

            int addedCount = 0;
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                var entry = catalog.Entries[i];
                if (entry == null)
                    continue;

                if (!TryNormalizeResourcePath(entry.ResourcePath, out string allowlistedPath))
                    continue;

                if (_developerPlacementAllowlist.Add(allowlistedPath))
                    addedCount++;
            }

            Debug.Log($"[BuildingController] Developer placement allowlist initialized with {_developerPlacementAllowlist.Count} paths (catalog additions={addedCount}).");
        }

        private static bool IsDeveloperPlacementPathAllowlisted(string normalizedPath, HashSet<string> allowlist)
        {
            if (string.IsNullOrWhiteSpace(normalizedPath))
                return false;

            if (allowlist == null || allowlist.Count == 0)
                return false;

            return allowlist.Contains(normalizedPath);
        }

        private static bool TryNormalizeResourcePath(string rawPath, out string normalizedPath)
        {
            normalizedPath = string.Empty;
            if (string.IsNullOrWhiteSpace(rawPath))
                return false;

            string trimmed = rawPath.Trim();
            string slashNormalized = trimmed.Replace('\\', '/');
            while (slashNormalized.Contains("//", StringComparison.Ordinal))
                slashNormalized = slashNormalized.Replace("//", "/", StringComparison.Ordinal);

            string withoutResourcesPrefix = slashNormalized.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase)
                ? slashNormalized.Substring("Resources/".Length)
                : slashNormalized;

            string withoutLeadingSlash = withoutResourcesPrefix.Trim('/');
            if (string.IsNullOrWhiteSpace(withoutLeadingSlash))
                return false;

            if (withoutLeadingSlash.Contains("..", StringComparison.Ordinal))
                return false;

            int extensionStart = withoutLeadingSlash.LastIndexOf('.');
            int finalSlash = withoutLeadingSlash.LastIndexOf('/');
            if (extensionStart > finalSlash)
                withoutLeadingSlash = withoutLeadingSlash.Substring(0, extensionStart);

            if (string.IsNullOrWhiteSpace(withoutLeadingSlash))
                return false;

            normalizedPath = withoutLeadingSlash;
            return true;
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
                BuildingPieceType.Floor => _floorPrefab,
                BuildingPieceType.Ramp => _rampPrefab,
                BuildingPieceType.Roof26 => _roof26Prefab,
                BuildingPieceType.Stairs => _stairsPrefab,
                BuildingPieceType.Pole => _polePrefab,
                BuildingPieceType.Beam => _beamPrefab,
                BuildingPieceType.AngledWall => _angledWallPrefab,
                BuildingPieceType.DoorFrame => _doorFramePrefab,
                BuildingPieceType.Window => _windowPrefab,
                BuildingPieceType.HalfWall => _halfWallPrefab,
                _ => null,
            };
        }

        /// <summary>Set building prefabs at generation time (called by PrefabGenerator).</summary>
        public void SetBuildingPrefabs(GameObject foundation, GameObject wall, GameObject floor = null, GameObject ramp = null, GameObject roof26 = null, GameObject stairs = null, GameObject pole = null, GameObject beam = null, GameObject angledWall = null, GameObject doorFrame = null, GameObject window = null, GameObject halfWall = null)
        {
            _foundationPrefab = foundation;
            _wallPrefab = wall;
            _floorPrefab = floor;
            _rampPrefab = ramp;
            _roof26Prefab = roof26;
            _stairsPrefab = stairs;
            _polePrefab = pole;
            _beamPrefab = beam;
            _angledWallPrefab = angledWall;
            _doorFramePrefab = doorFrame;
            _windowPrefab = window;
            _halfWallPrefab = halfWall;
        }

        /// <summary>Set recipes at generation time.</summary>
        public void SetRecipes(List<BuildingRecipe> recipes)
        {
            _recipes = recipes ?? new List<BuildingRecipe>();
        }
    }
}
