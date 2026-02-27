using System;
using System.Collections.Generic;
using ByteWar.Building;
using ByteWar.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ByteWar.UI
{
    public sealed partial class AssetDeployUI
    {
        private void DrawStatusHUD()
        {
            const float width = 520f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 80f;

            string activePath = _activeNode != null ? GetNodePath(_activeNode) : "Root";
            string selectedLabel = _selectedLeaf != null ? _selectedLeaf.DisplayName : "None";
            string text =
                "Asset Deploy (tap B browser)\n" +
                $"Node: {activePath} | Selected: {selectedLabel}\n" +
                (_status ?? string.Empty);

            GUI.Label(new Rect(x, y, width, 70f), text, _labelStyle);
        }

        private void DrawBrowser()
        {
            if (_activeNode == null)
                return;

            float panelWidth = Mathf.Min(560f, Screen.width - 24f);
            float panelHeight = Mathf.Min(520f, Screen.height - 120f);
            float panelX = (Screen.width - panelWidth) * 0.5f;
            float panelY = Mathf.Max(16f, (Screen.height - panelHeight) * 0.5f - 24f);

            GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, panelHeight), GUI.skin.box);
            GUILayout.Label($"Asset Browser · {GetNodePath(_activeNode)}", _headerStyle);
            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();
            GUI.enabled = _activeNode.Parent != null;
            if (GUILayout.Button("← Back", GUILayout.Width(96f)))
            {
                NavigateBrowserBack();
            }

            GUI.enabled = true;
            GUILayout.Label("LMB selects category/item · Esc closes + exits placement", _labelStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            float listHeight = Mathf.Max(120f, panelHeight * 0.45f);
            _browserScroll = GUILayout.BeginScrollView(_browserScroll, GUILayout.Height(listHeight));

            for (int i = 0; i < _activeNode.Children.Count; i++)
            {
                RadialNode child = _activeNode.Children[i];
                string prefix = child.Children.Count > 0 ? "▸ " : "• ";
                if (GUILayout.Button(prefix + child.DisplayName, _segmentStyle, GUILayout.Height(30f)))
                {
                    HandleBrowserNodeSelection(child);
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Space(8f);
            DrawBrowserPreviewArea(Mathf.Max(120f, panelHeight - listHeight - 78f));
            GUILayout.EndArea();
        }

        private void DrawBrowserPreviewArea(float previewHeight)
        {
            RadialNode previewNode = ResolveBrowserPreviewNode();

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(previewHeight), GUILayout.ExpandWidth(true));
            if (previewNode == null)
            {
                GUILayout.Label("Preview", _headerStyle);
                GUILayout.Label("No preview available in this category.", _labelStyle);
                GUILayout.EndVertical();
                return;
            }

            if (previewNode.LeafKind == RadialLeafKind.DeveloperAsset)
            {
                DrawDeveloperAssetPreview(previewNode, previewHeight);
                GUILayout.EndVertical();
                return;
            }

            DrawStructurePreview(previewNode);
            GUILayout.EndVertical();
        }

        private void DrawDeveloperAssetPreview(RadialNode previewNode, float previewHeight)
        {
            var entry = previewNode.CatalogEntry;
            string title = entry != null && !string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.DisplayName
                : previewNode.DisplayName;

            GUILayout.Label($"Preview · {title}", _headerStyle);

            Texture2D previewTexture = GetPreviewTexture(entry);
            float textureHeight = Mathf.Max(76f, previewHeight - 62f);
            Rect previewRect = GUILayoutUtility.GetRect(80f, textureHeight, GUILayout.ExpandWidth(true), GUILayout.Height(textureHeight));
            if (previewTexture != null)
            {
                GUI.DrawTexture(previewRect, previewTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.Box(previewRect, "No preview image");
            }

            string mode = IsDeveloperEntryServerPlacementReady(entry) ? "networked" : "local-only";
            GUILayout.Label($"Placement mode: {mode}", _labelStyle);
        }

        private void DrawStructurePreview(RadialNode previewNode)
        {
            GUILayout.Label($"Preview · {previewNode.DisplayName}", _headerStyle);
            GUILayout.Space(8f);
            GUILayout.Label("Select this structure to enter placement mode.", _labelStyle);
            GUILayout.Label("The in-world build preview appears immediately after selection.", _labelStyle);
        }

        private RadialNode ResolveBrowserPreviewNode()
        {
            if (_activeNode == null)
                return null;

            if (_selectedLeaf != null && IsNodeInBranch(_selectedLeaf, _activeNode))
                return _selectedLeaf;

            if (_activeNode.LeafKind != RadialLeafKind.None)
                return _activeNode;

            for (int i = 0; i < _activeNode.Children.Count; i++)
            {
                RadialNode leaf = _activeNode.Children[i].FindFirstLeaf();
                if (leaf != null)
                    return leaf;
            }

            return null;
        }

        private static bool IsNodeInBranch(RadialNode node, RadialNode branchRoot)
        {
            if (node == null || branchRoot == null)
                return false;

            RadialNode current = node;
            while (current != null)
            {
                if (ReferenceEquals(current, branchRoot))
                    return true;

                current = current.Parent;
            }

            return false;
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Clip,
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
            };

            _segmentStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                wordWrap = true,
            };
        }

        private void UpdatePreviewGhost()
        {
            if (_isBrowserOpen)
                DestroyPreviewGhost();

            if (_placementMode == PlacementMode.DeveloperAsset)
            {
                UpdateDeveloperPlacementPreviewGhost();
                return;
            }

            DestroyPreviewGhost();
        }

        private void UpdateDeveloperPlacementPreviewGhost()
        {
            if (_selectedEntry == null)
            {
                DestroyPreviewGhost();
                return;
            }

            string resourcePath = NormalizeResourceLoadPath(_selectedEntry.ResourcePath);
            var prefab = LoadResourcePrefab(resourcePath);
            if (prefab == null)
            {
                DestroyPreviewGhost();
                return;
            }

            EnsurePreviewGhost(prefab, $"dev:{resourcePath}");
            if (_previewGhost == null)
                return;

            _previewGhost.transform.position = _developerPlacementPoint;
            _previewGhost.transform.rotation = Quaternion.Euler(0f, _developerPlacementYaw, 0f);
            _previewGhost.transform.localScale = _previewGhostBaseScale;
        }

        private void EnsurePreviewGhost(GameObject prefab, string sourceKey)
        {
            if (_previewGhost != null && string.Equals(_previewGhostSourceKey, sourceKey, StringComparison.Ordinal))
                return;

            DestroyPreviewGhost();

            _previewGhost = Instantiate(prefab);
            _previewGhostSourceKey = sourceKey;
            _previewGhost.name = $"{prefab.name}_ASSET_DEPLOY_PREVIEW";

            _previewGhostBaseScale = _previewGhost.transform.localScale;
            _previewGhostRadialScale = 1f;

            SetLayerRecursively(_previewGhost, 2);

            foreach (var col in _previewGhost.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            foreach (var rb in _previewGhost.GetComponentsInChildren<Rigidbody>(true))
                rb.isKinematic = true;

            foreach (var netObj in _previewGhost.GetComponentsInChildren<NetworkObject>(true))
                netObj.enabled = false;

            foreach (var netBeh in _previewGhost.GetComponentsInChildren<NetworkBehaviour>(true))
                netBeh.enabled = false;

            foreach (var renderer in _previewGhost.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            if (TryExtractPreviewBounds(_previewGhost, out Bounds bounds))
            {
                _previewGhostRadialScale = ComputeRadialPreviewScale(bounds, _radialPreviewTargetSize, _radialPreviewMinScale, _radialPreviewMaxScale);
            }
        }

        private void DestroyPreviewGhost()
        {
            if (_previewGhost == null)
                return;

            Destroy(_previewGhost);
            _previewGhost = null;
            _previewGhostSourceKey = string.Empty;
            _previewGhostBaseScale = Vector3.one;
            _previewGhostRadialScale = 1f;
        }

        private void RefreshBuildingController()
        {
            if (_cachedBuildingController != null)
            {
                _cachedBuildingController.SetInternalBuildToggleInputEnabled(false);
                return;
            }

            if (Time.unscaledTime < _nextBuildingControllerSearchTime)
                return;

            _nextBuildingControllerSearchTime = Time.unscaledTime + 1f;

            var controllers = FindObjectsByType<BuildingController>(FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null && controllers[i].IsOwner)
                {
                    _cachedBuildingController = controllers[i];
                    _cachedBuildingController.SetInternalBuildToggleInputEnabled(false);
                    break;
                }
            }
        }

        private void RefreshRadialTreeIfNeeded()
        {
            if (_isBrowserOpen)
                return;

            if (Time.unscaledTime < _nextTreeRefreshTime)
                return;

            _nextTreeRefreshTime = Time.unscaledTime + 0.75f;
            string signature = ComputeTreeSignature();
            if (string.Equals(signature, _treeSignature, StringComparison.Ordinal))
                return;

            _treeSignature = signature;
            RebuildRadialTree();
        }

        private string ComputeTreeSignature()
        {
            int entryCount = _entries != null ? _entries.Count : 0;
            int recipeCount = (_cachedBuildingController != null && _cachedBuildingController.Recipes != null)
                ? _cachedBuildingController.Recipes.Count
                : 0;

            string lastRecipeName = string.Empty;
            if (_cachedBuildingController != null && _cachedBuildingController.Recipes != null && _cachedBuildingController.Recipes.Count > 0)
            {
                var last = _cachedBuildingController.Recipes[_cachedBuildingController.Recipes.Count - 1];
                lastRecipeName = last != null ? last.RecipeName : "null";
            }

            return $"{entryCount}|{recipeCount}|{lastRecipeName}";
        }

        private void RebuildRadialTree()
        {
            var root = new RadialNode("Root", RadialLeafKind.None);
            var structures = new RadialNode("Structures", RadialLeafKind.None);
            var developerAssets = new RadialNode("Developer Assets", RadialLeafKind.None);

            BuildStructuresBranch(structures);
            BuildDeveloperAssetsBranch(developerAssets);

            root.AddChild(structures);
            root.AddChild(developerAssets);
            root.SortRecursive();

            _rootNode = root;
            _activeNode = _rootNode;
        }

        private void BuildStructuresBranch(RadialNode structuresNode)
        {
            var foundationsNode = new RadialNode("Foundations & Access", RadialLeafKind.None);
            var wallsNode = new RadialNode("Walls & Openings", RadialLeafKind.None);
            var roofsNode = new RadialNode("Roofs & Supports", RadialLeafKind.None);

            structuresNode.AddChild(foundationsNode);
            structuresNode.AddChild(wallsNode);
            structuresNode.AddChild(roofsNode);

            var recipes = _cachedBuildingController != null ? _cachedBuildingController.Recipes : null;
            if (recipes != null && recipes.Count > 0)
            {
                for (int i = 0; i < recipes.Count; i++)
                {
                    var recipe = recipes[i];
                    if (recipe == null) continue;

                    RadialNode parent = GetStructureSubcategoryNode(recipe.PieceType, foundationsNode, wallsNode, roofsNode);
                    var leaf = RadialNode.CreateBuildingLeaf(recipe.RecipeName, recipe.PieceType, i);
                    parent.AddChild(leaf);
                }
            }
            else
            {
                foreach (BuildingPieceType pieceType in Enum.GetValues(typeof(BuildingPieceType)))
                {
                    RadialNode parent = GetStructureSubcategoryNode(pieceType, foundationsNode, wallsNode, roofsNode);
                    parent.AddChild(RadialNode.CreateBuildingLeaf(pieceType.ToString(), pieceType, -1));
                }
            }
        }

        private static RadialNode GetStructureSubcategoryNode(
            BuildingPieceType pieceType,
            RadialNode foundationsNode,
            RadialNode wallsNode,
            RadialNode roofsNode)
        {
            return pieceType switch
            {
                BuildingPieceType.Foundation => foundationsNode,
                BuildingPieceType.Floor => foundationsNode,
                BuildingPieceType.Ramp => foundationsNode,
                BuildingPieceType.Stairs => foundationsNode,
                BuildingPieceType.Wall => wallsNode,
                BuildingPieceType.AngledWall => wallsNode,
                BuildingPieceType.Window => wallsNode,
                BuildingPieceType.DoorFrame => wallsNode,
                BuildingPieceType.HalfWall => wallsNode,
                BuildingPieceType.Roof26 => roofsNode,
                BuildingPieceType.Pole => roofsNode,
                BuildingPieceType.Beam => roofsNode,
                _ => roofsNode,
            };
        }

        private void BuildDeveloperAssetsBranch(RadialNode developerNode)
        {
            if (_entries == null) return;

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ResourcePath))
                    continue;

                string normalizedResourcePath = NormalizeResourceLoadPath(entry.ResourcePath);
                GameObject prefab = LoadResourcePrefab(normalizedResourcePath);
                if (!HasDisplayableDeveloperComponents(prefab))
                    continue;

                string rp = normalizedResourcePath.Replace('\\', '/');
                if (rp.StartsWith($"{RootFolderName}/", StringComparison.OrdinalIgnoreCase))
                    rp = rp.Substring(RootFolderName.Length + 1);

                string[] parts = rp.Split('/');
                if (parts.Length == 0)
                    continue;

                RadialNode node = developerNode;
                for (int p = 0; p < parts.Length - 1; p++)
                {
                    string folder = parts[p];
                    if (string.IsNullOrWhiteSpace(folder))
                        continue;

                    node = node.GetOrCreateChild(folder);
                }

                string displayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? parts[parts.Length - 1] : entry.DisplayName;
                node.AddChild(RadialNode.CreateDeveloperLeaf(displayName, entry));
            }
        }

        internal static bool HasRequiredDeveloperSpawnComponents(GameObject prefab)
        {
            if (prefab == null)
                return false;

            return prefab.GetComponent<NetworkObject>() != null
                && prefab.GetComponentInChildren<Collider>(true) != null
                && prefab.GetComponentInChildren<LODGroup>(true) != null;
        }

        internal static bool HasDisplayableDeveloperComponents(GameObject prefab)
        {
            if (prefab == null)
                return false;

            return prefab.GetComponentInChildren<Collider>(true) != null
                && prefab.GetComponentInChildren<LODGroup>(true) != null;
        }

        private Texture2D GetPreviewTexture(DeployableAssetCatalog.Entry entry)
        {
            if (entry == null)
                return null;

            string previewResourcePath = NormalizeResourceLoadPath(entry.PreviewResourcePath);
            if (string.IsNullOrWhiteSpace(previewResourcePath))
                return null;

            if (_previewTextureCache.TryGetValue(previewResourcePath, out Texture2D cached))
                return cached;

            Texture2D loaded = Resources.Load<Texture2D>(previewResourcePath);
            _previewTextureCache[previewResourcePath] = loaded;
            return loaded;
        }

        private string GetNodePath(RadialNode node)
        {
            if (node == null)
                return "Root";

            var parts = new List<string>();
            RadialNode current = node;
            while (current != null)
            {
                if (!string.Equals(current.DisplayName, "Root", StringComparison.OrdinalIgnoreCase))
                    parts.Add(current.DisplayName);
                current = current.Parent;
            }

            parts.Reverse();
            return parts.Count == 0 ? "Root" : string.Join(" / ", parts);
        }

        private static bool IsBuildKeyHeldRaw()
        {
            var kb = Keyboard.current;
            if (kb != null)
                return kb.bKey.isPressed;

            return Input.GetKey(KeyCode.B);
        }

        private static bool WasBuildKeyPressed()
        {
            var kb = Keyboard.current;
            if (kb != null)
                return kb.bKey.wasPressedThisFrame;

            return Input.GetKeyDown(KeyCode.B);
        }

        private static Vector2 ReadMouseScreenPosition()
        {
            var m = Mouse.current;
            if (m != null)
                return m.position.ReadValue();

            return Input.mousePosition;
        }

        private static float ReadMouseWheelDelta()
        {
            var m = Mouse.current;
            if (m != null)
                return m.scroll.ReadValue().y;

            return Input.mouseScrollDelta.y;
        }

        private static bool WasLeftMousePressed()
        {
            var m = Mouse.current;
            if (m != null)
                return m.leftButton.wasPressedThisFrame;

            return Input.GetMouseButtonDown(0);
        }

        private static bool WasEscapePressed()
        {
            var kb = Keyboard.current;
            return kb != null ? kb.escapeKey.wasPressedThisFrame : Input.GetKeyDown(KeyCode.Escape);
        }

        private static bool WasBackspacePressed()
        {
            var kb = Keyboard.current;
            if (kb != null)
                return kb.backspaceKey.wasPressedThisFrame;

            return Input.GetKeyDown(KeyCode.Backspace);
        }

        private int GetRadialSegmentCount()
        {
            if (_activeNode == null)
                return 0;

            int count = _activeNode.Children.Count;
            if (_activeNode.Parent != null)
                count += 1;

            return count;
        }

        private bool IsBackSegmentIndex(int index)
        {
            if (_activeNode == null || _activeNode.Parent == null)
                return false;

            int backIndex = GetRadialSegmentCount() - 1;
            return index == backIndex;
        }

        private void NavigateRadialBack()
        {
            if (_activeNode == null || _activeNode.Parent == null)
                return;

            _activeNode = _activeNode.Parent;
            _status = $"Category: {GetNodePath(_activeNode)}";
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;

            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private GameObject LoadResourcePrefab(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                return null;

            if (_resourcePrefabCache.TryGetValue(resourcePath, out var cached))
                return cached;

            var loaded = Resources.Load<GameObject>(resourcePath);
            _resourcePrefabCache[resourcePath] = loaded;
            return loaded;
        }

        private void EnsureCatalogLoaded()
        {
            if (_entries != null && _entries.Count > 0)
                return;

            _catalog = Resources.Load<DeployableAssetCatalog>(CatalogResourcesLoadPath);
            _entries = _catalog != null ? _catalog.Entries : null;

            if (_entries == null || _entries.Count == 0)
            {
                _entries = new List<DeployableAssetCatalog.Entry>
                {
                    new DeployableAssetCatalog.Entry(FallbackDisplayName, FallbackPrefabResourcesPath, FallbackPreviewResourcesPath)
                };
            }
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
                    var entry = _entries[i];
                    if (entry != null && !string.IsNullOrEmpty(entry.ResourcePath))
                    {
                        _selectedEntry = entry;
                        return entry;
                    }
                }
            }

            _selectedEntry = new DeployableAssetCatalog.Entry(FallbackDisplayName, FallbackPrefabResourcesPath, FallbackPreviewResourcesPath);
            return _selectedEntry;
        }

        private void SetInteractionActive(bool active)
        {
            if (_interactionActive == active)
            {
                if (_interactionActive)
                {
                    MaintainCursorWhileActive();
                }
                return;
            }

            _interactionActive = active;
            if (_interactionActive)
            {
                CaptureCursor();
                MaintainCursorWhileActive();
                return;
            }

            ReleaseCursor();
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
    }
}
