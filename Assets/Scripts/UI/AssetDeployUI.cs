using System;
using System.Collections.Generic;
using ByteWar.Core;
using ByteWar.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ByteWar.UI
{
    /// <summary>
    /// In-game IMGUI panel to select and deploy test assets.
    /// Visible only while BOTH Shift and Tab are held; releasing either hides it.
    /// Shows a folder/subfolder view derived from the real Resources paths.
    /// Displays a preview thumbnail (if available) alongside the asset name.
    /// </summary>
    public sealed class AssetDeployUI : MonoBehaviour
    {
        private const string CatalogResourcesLoadPath = "Generated/DeployableAssetCatalog";
        private const string RootFolderName = "Generated";

        private const string FallbackDisplayName = "BlenderE2EProp";
        private const string FallbackPrefabResourcesPath = "Generated/BlenderE2EProp/BlenderE2EProp";
        private const string FallbackPreviewResourcesPath = "Generated/BlenderE2EProp/Preview";

        private bool _visible;

        [Header("Drag & Drop Placement")]
        [SerializeField] private LayerMask _dragRaycastMask = ~0;
        [SerializeField] private float _dragStartPixelThreshold = 8f;

        private bool _interactionActive;

        private bool _dragArmed;
        private DeployableAssetCatalog.Entry _armedEntry;
        private Vector2 _armedMouseDownScreenPos;

        private bool _isDragging;
        private DeployableAssetCatalog.Entry _dragEntry;
        private GameObject _dragGhost;
        private bool _dragPlacementValid;
        private Vector3 _dragPlacementPoint;

        private bool _cursorCaptured;
        private CursorLockMode _prevCursorLockState;
        private bool _prevCursorVisible;

        private bool _inputSuppressed;
        private PlayerInputHandler _cachedInputHandler;
        private float _nextInputHandlerSearchTime;

        private DeployableAssetCatalog _catalog;
        private IReadOnlyList<DeployableAssetCatalog.Entry> _entries;
        private FolderNode _root;
        private readonly List<string> _currentPath = new();
        private DeployableAssetCatalog.Entry _selectedEntry;

        private readonly Dictionary<string, Texture2D> _previewCache = new(StringComparer.OrdinalIgnoreCase);

        private Vector2 _scroll;
        private string _status;

        private GUIStyle _headerStyle;
        private GUIStyle _breadcrumbStyle;
        private GUIStyle _folderButtonStyle;
        private GUIStyle _assetButtonStyle;

        public static bool IsOpenShortcutHeld(bool shiftHeld, bool tabHeld)
        {
            return shiftHeld && tabHeld;
        }

        internal static bool ComputePanelVisible(bool shortcutHeld, bool isDragging)
        {
            return shortcutHeld && !isDragging;
        }

        internal static bool ComputeInteractionActive(bool shortcutHeld, bool dragArmed, bool isDragging)
        {
            return shortcutHeld || dragArmed || isDragging;
        }

        private void Awake()
        {
            EnsureCatalogLoaded();
            EnsureDefaultSelection();
        }

        private void Update()
        {
            bool shiftHeld = IsShiftHeldRaw();
            bool tabHeld = IsTabHeldRaw();
            bool shortcutHeld = IsOpenShortcutHeld(shiftHeld, tabHeld);

            // Panel visibility is only while the shortcut is held.
            // Interaction (cursor + input suppression) also stays active while dragging.
            _visible = ComputePanelVisible(shortcutHeld, _isDragging);
            SetInteractionActive(ComputeInteractionActive(shortcutHeld, _dragArmed, _isDragging));

            UpdateDragAndDrop();
        }

        private void OnDisable()
        {
            _visible = false;
            _dragArmed = false;
            _isDragging = false;
            DestroyDragGhost();
            SetInteractionActive(false);
        }

        private void OnGUI()
        {
            if (!_visible) return;

            EnsureCatalogLoaded();

            EnsureStyles();

            const float pad = 10f;
            float w = Mathf.Min(720f, Screen.width - (pad * 2f));
            float h = Mathf.Min(520f, Screen.height - (pad * 2f));

            float x = Mathf.Max(pad, (Screen.width - w) * 0.5f);
            float y = Mathf.Max(pad, (Screen.height - h) * 0.5f);

            GUI.Box(new Rect(x, y, w, h), string.Empty);

            GUILayout.BeginArea(new Rect(x + 10f, y + 10f, w - 20f, h - 20f));
            GUILayout.Label("Asset Deploy (hold Shift+Tab)", _headerStyle);

            GUILayout.Space(6f);
            DrawBreadcrumb();

            GUILayout.Space(6f);

            var node = GetCurrentNode();
            if (node == null)
            {
                _currentPath.Clear();
                node = _root;
            }

            if (_selectedEntry != null)
                GUILayout.Label($"Selected: {_selectedEntry.DisplayName}");

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            // Folders
            if (node != null && node.SortedFolderNames.Count > 0)
            {
                for (int i = 0; i < node.SortedFolderNames.Count; i++)
                {
                    string folderName = node.SortedFolderNames[i];
                    if (GUILayout.Button($"[Folder] {folderName}", _folderButtonStyle, GUILayout.Height(26f)))
                    {
                        _currentPath.Add(folderName);
                        _scroll = Vector2.zero;
                        GUI.FocusControl(null);
                    }
                }

                GUILayout.Space(6f);
            }

            // Assets
            if (node != null && node.SortedAssets.Count > 0)
            {
                for (int i = 0; i < node.SortedAssets.Count; i++)
                    DrawAssetRow(node.SortedAssets[i]);
            }
            else
            {
                GUILayout.Label("No assets in this folder.");
            }

            GUILayout.EndScrollView();

            GUILayout.Space(6f);

            if (GUILayout.Button("Deploy near player", GUILayout.Height(30f)))
            {
                if (TryDeployNearLocalPlayer(out var spawned, out var msg))
                {
                    _status = $"Deployed {spawned.name}";
                    Debug.Log($"[AssetDeployUI] {_status} at {spawned.transform.position}");
                }
                else
                {
                    _status = msg;
                    Debug.LogWarning($"[AssetDeployUI] Deploy failed: {msg}");
                }
            }

            GUILayout.Space(4f);
            GUILayout.Label(_status ?? string.Empty);
            GUILayout.EndArea();
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
            string resourcePath = entry.ResourcePath;

            var prefab = Resources.Load<GameObject>(resourcePath);
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

        private void EnsureStyles()
        {
            if (_headerStyle != null) return;
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14,
            };

            _breadcrumbStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
            };

            _folderButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
            };

            _assetButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                wordWrap = true,
            };
        }

        private void DrawBreadcrumb()
        {
            GUILayout.BeginHorizontal();

            bool hasParent = _currentPath.Count > 0;
            if (hasParent && GUILayout.Button("Up", GUILayout.Width(44f)))
            {
                _currentPath.RemoveAt(_currentPath.Count - 1);
                _scroll = Vector2.zero;
            }

            if (GUILayout.Button(RootFolderName, _breadcrumbStyle, GUILayout.Width(100f)))
            {
                _currentPath.Clear();
                _scroll = Vector2.zero;
            }

            for (int i = 0; i < _currentPath.Count; i++)
            {
                GUILayout.Label("/", GUILayout.Width(10f));
                string segment = _currentPath[i];
                if (GUILayout.Button(segment, _breadcrumbStyle))
                {
                    // Trim to this segment.
                    int removeCount = _currentPath.Count - (i + 1);
                    if (removeCount > 0)
                        _currentPath.RemoveRange(i + 1, removeCount);
                    _scroll = Vector2.zero;
                }
            }

            GUILayout.EndHorizontal();
        }

        private void DrawAssetRow(DeployableAssetCatalog.Entry entry)
        {
            if (entry == null) return;

            GUILayout.BeginHorizontal(GUILayout.Height(66f));

            Rect r = GUILayoutUtility.GetRect(64f, 64f, GUILayout.Width(64f), GUILayout.Height(64f));
            var tex = GetPreviewTexture(entry);
            if (tex != null)
                GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
            else
                GUI.Box(r, string.Empty);

            bool isSelected = ReferenceEquals(_selectedEntry, entry);
            Color oldBg = GUI.backgroundColor;
            if (isSelected) GUI.backgroundColor = new Color(0.75f, 0.95f, 0.75f, 1f);

            Rect labelRect = GUILayoutUtility.GetRect(
                new GUIContent(entry.DisplayName),
                _assetButtonStyle,
                GUILayout.Height(64f),
                GUILayout.ExpandWidth(true));

            GUI.Box(labelRect, entry.DisplayName, _assetButtonStyle);

            // Click-to-select, click-drag to place.
            var e = Event.current;
            if (e != null && e.type == EventType.MouseDown && e.button == 0 && labelRect.Contains(e.mousePosition))
            {
                _selectedEntry = entry;
                _status = $"Selected {entry.DisplayName}";

                ArmDrag(entry);
                e.Use();
            }

            GUI.backgroundColor = oldBg;

            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
        }

        private void ArmDrag(DeployableAssetCatalog.Entry entry)
        {
            if (entry == null) return;

            _dragArmed = true;
            _armedEntry = entry;
            _armedMouseDownScreenPos = ReadMouseScreenPosition();
        }

        private void UpdateDragAndDrop()
        {
            // Arm phase: wait for cursor movement while LMB is still held.
            if (_dragArmed && !_isDragging)
            {
                bool lmbHeld = IsLeftMousePressed();
                if (!lmbHeld)
                {
                    _dragArmed = false;
                    _armedEntry = null;
                    return;
                }

                Vector2 now = ReadMouseScreenPosition();
                float dist = Vector2.Distance(now, _armedMouseDownScreenPos);
                if (dist >= _dragStartPixelThreshold)
                {
                    StartDrag(_armedEntry);
                }
            }

            if (!_isDragging)
                return;

            // Drag phase: ghost follows cursor ray; release LMB to place.
            EnsureCatalogLoaded();
            EnsureDefaultSelection();

            UpdateDragRaycast();
            EnsureDragGhost();

            if (_dragGhost != null)
            {
                _dragGhost.transform.position = _dragPlacementPoint;
                _dragGhost.transform.rotation = GetDragRotation();
            }

            // Cancel
            if (WasCancelPressed())
            {
                _status = "Drag cancelled.";
                StopDrag();
                return;
            }

            // Drop
            if (WasLeftMouseReleased())
            {
                if (!_dragPlacementValid)
                {
                    _status = "Invalid placement (no surface hit).";
                    StopDrag();
                    return;
                }

                if (TryDeployAt(_dragPlacementPoint, GetDragRotation(), out var spawned, out var msg))
                {
                    _status = $"Deployed {spawned.name}";
                }
                else
                {
                    _status = msg;
                }

                StopDrag();
            }
        }

        private void StartDrag(DeployableAssetCatalog.Entry entry)
        {
            _dragArmed = false;
            _armedEntry = null;

            _dragEntry = entry;
            _isDragging = _dragEntry != null;
        }

        private void StopDrag()
        {
            _isDragging = false;
            _dragEntry = null;
            DestroyDragGhost();
        }

        private void EnsureDragGhost()
        {
            if (_dragGhost != null)
                return;

            if (_dragEntry == null || string.IsNullOrEmpty(_dragEntry.ResourcePath))
                return;

            var prefab = Resources.Load<GameObject>(_dragEntry.ResourcePath);
            if (prefab == null)
                return;

            _dragGhost = Instantiate(prefab);
            _dragGhost.name = $"{prefab.name}_DRAG_GHOST";

            // Make sure the ghost cannot be raycast-hit and cannot interact with physics/network.
            SetLayerRecursively(_dragGhost, 2 /* Ignore Raycast */);

            foreach (var col in _dragGhost.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
            foreach (var rb in _dragGhost.GetComponentsInChildren<Rigidbody>(true))
                rb.isKinematic = true;

            foreach (var netObj in _dragGhost.GetComponentsInChildren<NetworkObject>(true))
                netObj.enabled = false;
            foreach (var netBeh in _dragGhost.GetComponentsInChildren<NetworkBehaviour>(true))
                netBeh.enabled = false;

            foreach (var r in _dragGhost.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        private void DestroyDragGhost()
        {
            if (_dragGhost == null) return;
            Destroy(_dragGhost);
            _dragGhost = null;
        }

        private void UpdateDragRaycast()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                _dragPlacementValid = false;
                _dragPlacementPoint = Vector3.zero;
                return;
            }

            Vector2 mouse = ReadMouseScreenPosition();
            Ray ray = cam.ScreenPointToRay(new Vector3(mouse.x, mouse.y, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, 250f, _dragRaycastMask, QueryTriggerInteraction.Ignore))
            {
                _dragPlacementValid = true;
                _dragPlacementPoint = hit.point;
                _dragPlacementPoint.y += 0.02f;
            }
            else
            {
                _dragPlacementValid = false;
                _dragPlacementPoint = ray.origin + ray.direction * 8f;
            }
        }

        private Quaternion GetDragRotation()
        {
            Camera cam = Camera.main;
            if (cam == null) return Quaternion.identity;
            Vector3 e = cam.transform.eulerAngles;
            return Quaternion.Euler(0f, e.y, 0f);
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static bool IsShiftHeldRaw()
        {
            var kb = Keyboard.current;
            if (kb != null)
                return kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        private static bool IsTabHeldRaw()
        {
            var kb = Keyboard.current;
            if (kb != null)
                return kb.tabKey.isPressed;

            return Input.GetKey(KeyCode.Tab);
        }

        private static Vector2 ReadMouseScreenPosition()
        {
            var m = Mouse.current;
            if (m != null)
                return m.position.ReadValue();

            return Input.mousePosition;
        }

        private static bool IsLeftMousePressed()
        {
            var m = Mouse.current;
            if (m != null)
                return m.leftButton.isPressed;

            return Input.GetMouseButton(0);
        }

        private static bool WasLeftMouseReleased()
        {
            var m = Mouse.current;
            if (m != null)
                return m.leftButton.wasReleasedThisFrame;

            return Input.GetMouseButtonUp(0);
        }

        private static bool WasCancelPressed()
        {
            var kb = Keyboard.current;
            var m = Mouse.current;

            bool esc = kb != null ? kb.escapeKey.wasPressedThisFrame : Input.GetKeyDown(KeyCode.Escape);
            bool rmb = m != null ? m.rightButton.wasPressedThisFrame : Input.GetMouseButtonDown(1);
            return esc || rmb;
        }

        private Texture2D GetPreviewTexture(DeployableAssetCatalog.Entry entry)
        {
            string previewPath = entry.PreviewResourcePath;
            if (string.IsNullOrEmpty(previewPath))
                return null;

            if (_previewCache.TryGetValue(previewPath, out var cached))
                return cached;

            var loaded = Resources.Load<Texture2D>(previewPath);
            _previewCache[previewPath] = loaded;
            return loaded;
        }

        private void EnsureCatalogLoaded()
        {
            if (_root != null && _entries != null && _entries.Count > 0)
                return;

            _catalog = Resources.Load<DeployableAssetCatalog>(CatalogResourcesLoadPath);
            _entries = _catalog != null ? _catalog.Entries : null;

            if (_entries == null || _entries.Count == 0)
            {
                // Fallback for repos/builds where the catalog asset hasn't been generated yet.
                _entries = new List<DeployableAssetCatalog.Entry>
                {
                    new DeployableAssetCatalog.Entry(FallbackDisplayName, FallbackPrefabResourcesPath, FallbackPreviewResourcesPath)
                };
            }

            _root = BuildFolderTree(_entries);
        }

        private void EnsureDefaultSelection()
        {
            if (_selectedEntry != null && !string.IsNullOrEmpty(_selectedEntry.ResourcePath))
                return;

            _selectedEntry = GetSelectedEntryOrDefault();
        }

        private DeployableAssetCatalog.Entry GetSelectedEntryOrDefault()
        {
            if (_selectedEntry != null && !string.IsNullOrEmpty(_selectedEntry.ResourcePath))
                return _selectedEntry;

            if (_entries != null)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    var e = _entries[i];
                    if (e != null && !string.IsNullOrEmpty(e.ResourcePath))
                    {
                        _selectedEntry = e;
                        return e;
                    }
                }
            }

            _selectedEntry = new DeployableAssetCatalog.Entry(FallbackDisplayName, FallbackPrefabResourcesPath, FallbackPreviewResourcesPath);
            return _selectedEntry;
        }

        private static FolderNode BuildFolderTree(IReadOnlyList<DeployableAssetCatalog.Entry> entries)
        {
            var root = new FolderNode();
            if (entries == null) return root;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null) continue;
                if (string.IsNullOrEmpty(entry.ResourcePath)) continue;

                string rp = entry.ResourcePath.Replace('\\', '/');
                if (rp.StartsWith($"{RootFolderName}/", StringComparison.OrdinalIgnoreCase))
                    rp = rp.Substring(RootFolderName.Length + 1);

                string[] parts = rp.Split('/');
                if (parts.Length == 0) continue;

                var node = root;
                for (int p = 0; p < parts.Length - 1; p++)
                {
                    string folder = parts[p];
                    if (string.IsNullOrEmpty(folder)) continue;
                    node = node.GetOrCreate(folder);
                }

                node.Assets.Add(entry);
            }

            root.SortRecursive();
            return root;
        }

        private FolderNode GetCurrentNode()
        {
            var node = _root;
            for (int i = 0; i < _currentPath.Count; i++)
            {
                if (node == null) return null;
                string seg = _currentPath[i];
                if (!node.Folders.TryGetValue(seg, out var next))
                    return null;
                node = next;
            }

            return node;
        }

        private void SetInteractionActive(bool active)
        {
            if (_interactionActive == active)
            {
                if (_interactionActive)
                {
                    MaintainCursorWhileActive();
                    TryApplyInputSuppressionWhileVisible();
                }
                return;
            }

            _interactionActive = active;

            if (_interactionActive)
            {
                CaptureCursor();
                MaintainCursorWhileActive();
                TryApplyInputSuppressionWhileVisible();
                return;
            }

            ReleaseCursor();
            ReleaseInputSuppression();
        }

        private void MaintainCursorWhileActive()
        {
            if (!_cursorCaptured) return;

            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible)
                Cursor.visible = true;
        }

        private void CaptureCursor()
        {
            if (_cursorCaptured) return;
            _cursorCaptured = true;

            _prevCursorLockState = Cursor.lockState;
            _prevCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ReleaseCursor()
        {
            if (!_cursorCaptured) return;
            _cursorCaptured = false;

            Cursor.lockState = _prevCursorLockState;
            Cursor.visible = _prevCursorVisible;
        }

        private void TryApplyInputSuppressionWhileVisible()
        {
            if (_inputSuppressed && _cachedInputHandler != null)
                return;

            if (Time.unscaledTime < _nextInputHandlerSearchTime)
                return;

            _nextInputHandlerSearchTime = Time.unscaledTime + 1f;

            if (_cachedInputHandler == null)
                _cachedInputHandler = FindFirstObjectByType<PlayerInputHandler>();

            if (_cachedInputHandler == null)
                return;

            _cachedInputHandler.SetInputSuppressed(this, true);
            _inputSuppressed = true;
        }

        private void ReleaseInputSuppression()
        {
            if (!_inputSuppressed) return;
            _inputSuppressed = false;

            if (_cachedInputHandler != null)
                _cachedInputHandler.SetInputSuppressed(this, false);
        }

        private sealed class FolderNode
        {
            public readonly Dictionary<string, FolderNode> Folders = new(StringComparer.OrdinalIgnoreCase);
            public readonly List<DeployableAssetCatalog.Entry> Assets = new();

            public readonly List<string> SortedFolderNames = new();
            public readonly List<DeployableAssetCatalog.Entry> SortedAssets = new();

            public FolderNode GetOrCreate(string name)
            {
                if (!Folders.TryGetValue(name, out var child))
                {
                    child = new FolderNode();
                    Folders[name] = child;
                }

                return child;
            }

            public void SortRecursive()
            {
                SortedFolderNames.Clear();
                SortedAssets.Clear();

                foreach (var kvp in Folders)
                    SortedFolderNames.Add(kvp.Key);
                SortedFolderNames.Sort(StringComparer.OrdinalIgnoreCase);

                SortedAssets.AddRange(Assets);
                SortedAssets.Sort((a, b) => string.CompareOrdinal(a?.DisplayName, b?.DisplayName));

                foreach (var kvp in Folders)
                    kvp.Value.SortRecursive();
            }
        }
    }
}
