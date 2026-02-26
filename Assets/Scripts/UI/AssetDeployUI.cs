using System;
using System.Collections.Generic;
using ByteWar.Building;
using ByteWar.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ByteWar.UI
{
    /// <summary>
    /// Tap-B deploy browser with nested categories.
    /// Click categories/items to drill and select deploy targets.
    /// Leaf selection enters placement mode and remains active until cancelled.
    /// </summary>
    public sealed partial class AssetDeployUI : MonoBehaviour
    {
        private const string CatalogResourcesLoadPath = "Generated/DeployableAssetCatalog";
        private const string RootFolderName = "Generated";

        private const string FallbackDisplayName = "BlenderE2EProp";
        private const string FallbackPrefabResourcesPath = "Generated/BlenderE2EProp/BlenderE2EProp";
        private const string FallbackPreviewResourcesPath = "Generated/BlenderE2EProp/Preview";

        [Header("Placement")]
        [SerializeField] private LayerMask _placementRaycastMask = ~0;
        [SerializeField] private float _placementRaycastDistance = 250f;
        [SerializeField] private float _placementYOffset = 0.02f;
        [SerializeField] private float _placementRotationStep = 15f;

        [Header("3D Preview")]
        [SerializeField] private float _radialPreviewTargetSize = 1.6f;
        [SerializeField] private float _radialPreviewMinScale = 0.35f;
        [SerializeField] private float _radialPreviewMaxScale = 2.5f;

        private bool _interactionActive;
        private bool _cursorCaptured;
        private CursorLockMode _prevCursorLockState;
        private bool _prevCursorVisible;

        private BuildingController _cachedBuildingController;
        private float _nextBuildingControllerSearchTime;
        private float _nextTreeRefreshTime;
        private string _treeSignature;

        private DeployableAssetCatalog _catalog;
        private IReadOnlyList<DeployableAssetCatalog.Entry> _entries;
        private DeployableAssetCatalog.Entry _selectedEntry;

        private RadialNode _rootNode;
        private RadialNode _activeNode;
        private RadialNode _selectedLeaf;

        private bool _isBrowserOpen;
        private Vector2 _browserScroll;

        private PlacementMode _placementMode;
        private float _developerPlacementYaw;
        private bool _developerPlacementValid;
        private Vector3 _developerPlacementPoint;

        private GameObject _previewGhost;
        private string _previewGhostSourceKey;
        private Vector3 _previewGhostBaseScale = Vector3.one;
        private float _previewGhostRadialScale = 1f;

        private string _status;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _segmentStyle;

        private readonly Dictionary<string, GameObject> _resourcePrefabCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Texture2D> _previewTextureCache = new(StringComparer.OrdinalIgnoreCase);

        private enum PlacementMode
        {
            None = 0,
            DeveloperAsset = 1,
            BuildingRecipe = 2,
        }

        private enum RadialLeafKind
        {
            None = 0,
            DeveloperAsset = 1,
            BuildingRecipe = 2,
        }

        public static bool IsOpenShortcutPressed(bool bPressedThisFrame)
        {
            return bPressedThisFrame;
        }

        internal static Vector2 ComputeFlickVector(Vector2 holdStart, Vector2 holdEnd)
        {
            return holdEnd - holdStart;
        }

        internal static int ResolveRadialSegmentIndex(Vector2 flickVector, int segmentCount)
        {
            if (segmentCount <= 0) return -1;
            if (flickVector.sqrMagnitude <= 0.0001f) return -1;

            float mirroredX = -flickVector.x;
            float angle = Mathf.Atan2(flickVector.y, mirroredX);
            if (angle < 0f)
                angle += Mathf.PI * 2f;

            float segmentSize = (Mathf.PI * 2f) / segmentCount;
            int index = Mathf.FloorToInt(angle / segmentSize);
            return Mathf.Clamp(index, 0, segmentCount - 1);
        }

        internal static bool IsValidSegment(int index, int segmentCount)
        {
            return index >= 0 && index < segmentCount;
        }

        internal static Vector2 ResolveRadialDrawDirection(int segmentIndex, int segmentCount)
        {
            if (segmentCount <= 0)
                return Vector2.right;

            int wrappedIndex = ((segmentIndex % segmentCount) + segmentCount) % segmentCount;
            float angle = ((Mathf.PI * 2f) / segmentCount) * wrappedIndex;
            return new Vector2(-Mathf.Cos(angle), -Mathf.Sin(angle));
        }

        internal static bool TryExtractPreviewBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
                return false;

            bool hasBounds = false;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                Bounds current = renderer.bounds;
                if (current.size.sqrMagnitude <= 0.0001f)
                    continue;

                if (!hasBounds)
                {
                    bounds = current;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(current.min);
                    bounds.Encapsulate(current.max);
                }
            }

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null)
                    continue;

                Bounds current = collider.bounds;
                if (current.size.sqrMagnitude <= 0.0001f)
                    continue;

                if (!hasBounds)
                {
                    bounds = current;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(current.min);
                    bounds.Encapsulate(current.max);
                }
            }

            return hasBounds;
        }

        internal static float ComputeRadialPreviewScale(Bounds bounds, float targetSize, float minScale, float maxScale)
        {
            float clampedTargetSize = Mathf.Max(0.01f, targetSize);
            float clampedMinScale = Mathf.Max(0.01f, minScale);
            float clampedMaxScale = Mathf.Max(clampedMinScale, maxScale);

            float maxExtent = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (maxExtent <= 0.0001f)
                return 1f;

            float rawScale = clampedTargetSize / maxExtent;
            return Mathf.Clamp(rawScale, clampedMinScale, clampedMaxScale);
        }

        internal static string NormalizeResourceLoadPath(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                return string.Empty;

            string normalized = resourcePath.Trim().Replace('\\', '/');

            int slash = normalized.LastIndexOf('/');
            int dot = normalized.LastIndexOf('.');
            if (dot > slash)
                normalized = normalized.Substring(0, dot);

            return normalized;
        }

        private void Awake()
        {
            EnsureCatalogLoaded();
            RefreshBuildingController();
            RebuildRadialTree();
            EnsureDefaultSelection();
        }

        private void Update()
        {
            EnsureCatalogLoaded();
            RefreshBuildingController();
            RefreshRadialTreeIfNeeded();

            HandleBrowserInput();
            if (_cachedBuildingController != null)
                _cachedBuildingController.SetExternalInputSuppressed(_isBrowserOpen);

            HandlePlacementFlow();
            UpdatePreviewGhost();

            bool needsCapturedInteraction = _isBrowserOpen || _placementMode == PlacementMode.DeveloperAsset;
            SetInteractionActive(needsCapturedInteraction);
        }

        private void OnDisable()
        {
            _isBrowserOpen = false;
            _placementMode = PlacementMode.None;
            if (_cachedBuildingController != null)
                _cachedBuildingController.SetExternalInputSuppressed(false);
            DestroyPreviewGhost();
            SetInteractionActive(false);
        }

        private void OnGUI()
        {
            EnsureStyles();

            DrawStatusHUD();

            if (!_isBrowserOpen || _activeNode == null)
                return;

            DrawBrowser();
        }

        public bool TryDeployNearLocalPlayer(out GameObject spawned, out string message)
        {
            spawned = null;
            message = string.Empty;

            Transform anchor = null;
            var player = FindFirstObjectByType<NetworkPlayer>();
            if (player != null) anchor = player.transform;

            if (anchor == null && Camera.main != null) anchor = Camera.main.transform;

            if (anchor == null)
            {
                message = "No NetworkPlayer or Camera.main found.";
                return false;
            }

            Vector3 fwd = anchor.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();

            Vector3 pos = anchor.position + fwd * 3f;
            pos.y = 0.2f;
            Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);

            return TryDeployAt(pos, rot, out spawned, out message);
        }

        public bool TryDeployAt(Vector3 position, Quaternion rotation, out GameObject spawned, out string message)
        {
            spawned = null;
            message = string.Empty;

            EnsureCatalogLoaded();
            EnsureDefaultSelection();

            var entry = GetSelectedEntryOrDefault();
            string resourcePath = NormalizeResourceLoadPath(entry.ResourcePath);
            var prefab = LoadResourcePrefab(resourcePath);
            if (prefab == null)
            {
                message = $"Resources prefab missing: {resourcePath}";
                return false;
            }

            spawned = Instantiate(prefab, position, rotation);
            spawned.name = string.IsNullOrEmpty(entry.DisplayName) ? prefab.name : entry.DisplayName;

            if (spawned.GetComponent<LODGroup>() == null)
            {
                message = "Spawned asset missing LODGroup.";
                Destroy(spawned);
                spawned = null;
                return false;
            }

            if (spawned.GetComponent<Collider>() == null)
            {
                message = "Spawned asset missing root collider.";
                Destroy(spawned);
                spawned = null;
                return false;
            }

            message = "OK";
            return true;
        }

        private void HandleBrowserInput()
        {
            if (WasEscapePressed())
            {
                HandleEscapeAction();
                return;
            }

            if (!IsOpenShortcutPressed(WasBuildKeyPressed()))
                return;

            OpenBrowser();
        }

        private void OpenBrowser()
        {
            EnsureCatalogLoaded();
            RefreshBuildingController();

            if (_activeNode == null)
                _activeNode = _rootNode;

            _isBrowserOpen = true;
            _browserScroll = Vector2.zero;
            _status = $"Asset browser: {GetNodePath(_activeNode)}";
        }

        private void CloseBrowser(string status = null)
        {
            if (!_isBrowserOpen)
                return;

            _isBrowserOpen = false;
            if (!string.IsNullOrWhiteSpace(status))
                _status = status;
        }

        private void HandleEscapeAction()
        {
            bool hadBrowserOpen = _isBrowserOpen;
            if (hadBrowserOpen)
                CloseBrowser();

            if (_placementMode != PlacementMode.None)
            {
                CancelPlacementMode();
                return;
            }

            if (hadBrowserOpen)
                _status = "Asset browser closed.";
        }

        private void HandleBrowserNodeSelection(RadialNode chosen)
        {
            if (chosen == null)
                return;

            if (chosen.Children.Count > 0)
            {
                _activeNode = chosen;
                _status = $"Category: {GetNodePath(chosen)}";
                return;
            }

            if (chosen.LeafKind == RadialLeafKind.None)
                return;

            SelectLeaf(chosen);
            CloseBrowser();
        }

        internal bool TrySelectBrowserNodeForTests(string displayName)
        {
            if (_activeNode == null || string.IsNullOrWhiteSpace(displayName))
                return false;

            for (int i = 0; i < _activeNode.Children.Count; i++)
            {
                RadialNode child = _activeNode.Children[i];
                if (!string.Equals(child.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                    continue;

                HandleBrowserNodeSelection(child);
                return true;
            }

            return false;
        }

        private void NavigateBrowserBack()
        {
            if (_activeNode == null || _activeNode.Parent == null)
                return;

            _activeNode = _activeNode.Parent;
            _status = $"Category: {GetNodePath(_activeNode)}";
        }

        internal bool IsBrowserOpenForTests => _isBrowserOpen;
        internal bool IsPlacementModeActiveForTests => _placementMode != PlacementMode.None;

        internal void OpenBrowserForTests()
        {
            OpenBrowser();
        }

        internal void EnterDeveloperPlacementModeForTests(DeployableAssetCatalog.Entry entry)
        {
            _selectedEntry = entry;
            EnterDeveloperPlacementMode();
        }

        internal void SelectDeveloperEntryFromBrowserForTests(DeployableAssetCatalog.Entry entry)
        {
            _isBrowserOpen = true;
            _selectedEntry = entry;
            EnterDeveloperPlacementMode();
            CloseBrowser();
        }

        internal void HandleEscapeForTests()
        {
            HandleEscapeAction();
        }

        private void SelectLeaf(RadialNode node)
        {
            _selectedLeaf = node;

            if (node.LeafKind == RadialLeafKind.DeveloperAsset)
            {
                _selectedEntry = node.CatalogEntry;
                EnterDeveloperPlacementMode();
                return;
            }

            if (node.LeafKind == RadialLeafKind.BuildingRecipe)
            {
                EnterBuildingPlacementMode(node.BuildingPieceType, node.PreferredRecipeIndex, node.DisplayName);
            }
        }

        private void EnterDeveloperPlacementMode()
        {
            if (_selectedEntry == null)
            {
                _status = "Developer asset leaf missing catalog entry.";
                return;
            }

            if (_cachedBuildingController != null)
            {
                _cachedBuildingController.SetExternalRightClickCancelMode(false);
                if (_cachedBuildingController.IsBuildModeActive)
                    _cachedBuildingController.SetBuildModeActive(false);
            }

            _placementMode = PlacementMode.DeveloperAsset;
            _developerPlacementYaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f;
            string placementModeLabel = IsDeveloperEntryServerPlacementReady(_selectedEntry)
                ? "networked"
                : "local-only";
            _status = $"Developer placement ({placementModeLabel}): {_selectedEntry.DisplayName} (LMB place, wheel rotate, Esc cancel).";
        }

        private void EnterBuildingPlacementMode(BuildingPieceType pieceType, int preferredRecipeIndex, string displayName)
        {
            if (!TryConfigureBuildingControllerForRecipe(pieceType, preferredRecipeIndex, out _, out string message))
            {
                _status = $"Building placement unavailable: {message}";
                return;
            }

            _placementMode = PlacementMode.BuildingRecipe;
            _status = $"Building placement: {displayName} (LMB place, wheel rotate, Esc cancel).";
        }

        private void HandlePlacementFlow()
        {
            if (_isBrowserOpen)
                return;

            if (_placementMode == PlacementMode.None)
                return;

            if (WasEscapePressed())
            {
                CancelPlacementMode();
                return;
            }

            if (_placementMode == PlacementMode.DeveloperAsset)
            {
                UpdateDeveloperPlacementInput();
            }
        }

        private void CancelPlacementMode()
        {
            if (_cachedBuildingController != null)
            {
                _cachedBuildingController.SetExternalRightClickCancelMode(false);
                if (_placementMode == PlacementMode.BuildingRecipe)
                    _cachedBuildingController.SetBuildModeActive(false);
            }

            _placementMode = PlacementMode.None;
            _selectedLeaf = null;
            _developerPlacementValid = false;
            _status = "Placement cancelled.";
        }

        private void UpdateDeveloperPlacementInput()
        {
            float wheel = ReadMouseWheelDelta();
            if (Mathf.Abs(wheel) > 0.01f)
            {
                float direction = wheel > 0f ? 1f : -1f;
                _developerPlacementYaw += direction * _placementRotationStep;
            }

            UpdateDeveloperPlacementPoint();

            if (!WasLeftMousePressed())
                return;

            if (_selectedEntry == null)
            {
                _status = "No developer asset selected.";
                return;
            }

            if (!_developerPlacementValid)
            {
                _status = "Invalid placement point.";
                return;
            }

            var rotation = Quaternion.Euler(0f, _developerPlacementYaw, 0f);
            if (TryPlaceDeveloperAsset(_selectedEntry, _developerPlacementPoint, rotation, out var spawned, out var message))
            {
                _status = spawned != null
                    ? $"Placed developer asset: {spawned.name}."
                    : "Developer placement request sent to server.";
            }
            else
            {
                _status = message;
            }
        }

        private void UpdateDeveloperPlacementPoint()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                _developerPlacementValid = false;
                _developerPlacementPoint = Vector3.zero;
                return;
            }

            Vector2 mouse = ReadMouseScreenPosition();
            Ray ray = cam.ScreenPointToRay(new Vector3(mouse.x, mouse.y, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, _placementRaycastDistance, _placementRaycastMask, QueryTriggerInteraction.Ignore))
            {
                _developerPlacementValid = true;
                _developerPlacementPoint = hit.point;
                _developerPlacementPoint.y += _placementYOffset;
            }
            else
            {
                _developerPlacementValid = false;
                _developerPlacementPoint = ray.origin + ray.direction * 8f;
            }
        }

        private bool TryPlaceDeveloperAsset(DeployableAssetCatalog.Entry entry, Vector3 position, Quaternion rotation, out GameObject spawned, out string message)
        {
            spawned = null;
            message = string.Empty;

            bool serverPlacementReady = IsDeveloperEntryServerPlacementReady(entry);

            RefreshBuildingController();
            if (_cachedBuildingController != null && serverPlacementReady)
            {
                string resourcePath = NormalizeResourceLoadPath(entry?.ResourcePath);
                string displayName = entry?.DisplayName ?? string.Empty;
                bool success = _cachedBuildingController.TryRequestDeveloperAssetPlacement(position, rotation, resourcePath, displayName);
                message = success ? "OK" : "Developer placement request rejected.";
                return success;
            }

            _selectedEntry = entry;
            bool localSuccess = TryDeployAt(position, rotation, out spawned, out message);
            if (localSuccess && _cachedBuildingController != null && !serverPlacementReady)
                message = "Placed locally (developer prefab is not network-ready).";

            return localSuccess;
        }

        private bool IsDeveloperEntryServerPlacementReady(DeployableAssetCatalog.Entry entry)
        {
            string resourcePath = NormalizeResourceLoadPath(entry?.ResourcePath);
            GameObject prefab = LoadResourcePrefab(resourcePath);
            return HasRequiredDeveloperSpawnComponents(prefab);
        }

        private bool TryConfigureBuildingControllerForRecipe(BuildingPieceType pieceType, int preferredRecipeIndex, out int selectedRecipeIndex, out string message)
        {
            selectedRecipeIndex = -1;
            message = string.Empty;

            RefreshBuildingController();
            if (_cachedBuildingController == null)
            {
                message = "Local BuildingController not found.";
                return false;
            }

            selectedRecipeIndex = ResolveRecipeIndex(_cachedBuildingController, pieceType, preferredRecipeIndex);
            if (selectedRecipeIndex < 0)
            {
                message = $"No recipe mapped for {pieceType}.";
                return false;
            }

            _cachedBuildingController.SetBuildModeActive(true);
            _cachedBuildingController.SetExternalRightClickCancelMode(true);
            _cachedBuildingController.SetExternalCancelOnRightClick(false);
            if (!_cachedBuildingController.TrySelectRecipeIndex(selectedRecipeIndex, clearPreview: true))
            {
                message = "Failed to select building recipe index.";
                return false;
            }

            message = "OK";
            return true;
        }

        private static int ResolveRecipeIndex(BuildingController controller, BuildingPieceType pieceType, int preferredRecipeIndex)
        {
            var recipes = controller.Recipes;
            if (recipes == null || recipes.Count == 0)
                return -1;

            if (preferredRecipeIndex >= 0 && preferredRecipeIndex < recipes.Count)
                return preferredRecipeIndex;

            for (int i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                if (recipe != null && recipe.PieceType == pieceType)
                    return i;
            }

            return -1;
        }

    }
}
