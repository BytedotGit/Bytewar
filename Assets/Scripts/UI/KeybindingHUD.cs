using UnityEngine;
using ByteWar.Building;
using ByteWar.Core;

namespace ByteWar.UI
{
    /// <summary>
    /// IMGUI overlay that shows keybinding hints. Always visible in the top-left corner.
    /// When build mode is active, also shows the current recipe selection and switching instructions.
    /// Toggle visibility with F2.
    /// </summary>
    public class KeybindingHUD : MonoBehaviour
    {
        private bool _visible = true;
        private GUIStyle _bgStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _activeStyle;

        private BuildingController _buildingController;
        private float _nextControllerSearch;

        private void Start()
        {
            Debug.Log("[KeybindingHUD] Initialized. Press F2 to toggle visibility.");
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.f2Key.wasPressedThisFrame)
            {
                _visible = !_visible;
                Debug.Log($"[KeybindingHUD] Visibility toggled: {_visible}");
            }

            // Lazily find the local player's BuildingController
            if (_buildingController == null && Time.time > _nextControllerSearch)
            {
                _nextControllerSearch = Time.time + 2f;
                var controllers = FindObjectsByType<BuildingController>(FindObjectsSortMode.None);
                foreach (var bc in controllers)
                {
                    if (bc.IsOwner)
                    {
                        _buildingController = bc;
                        break;
                    }
                }
            }
        }

        private void OnGUI()
        {
            // Only show in DevMode
            if (!GameConstants.IsDevMode()) return;
            if (!_visible) return;

            InitStyles();

            float padding = 10f;
            float width = 260f;
            float x = Screen.width - width - padding;
            float y = padding;

            // General keybindings
            GUILayout.BeginArea(new Rect(x, y, width, Screen.height - padding * 2));

            GUILayout.Box("", _bgStyle, GUILayout.Width(width), GUILayout.ExpandHeight(false));
            GUILayout.Space(-2); // overlap

            GUILayout.Label("KEYBINDINGS", _headerStyle);
            GUILayout.Space(4);
            GUILayout.Label("WASD / Arrows  –  Move", _labelStyle);
            GUILayout.Label("Space  –  Jump", _labelStyle);
            GUILayout.Label("Mouse  –  Look / Aim", _labelStyle);
            GUILayout.Label("E  –  Interact / Gather", _labelStyle);
            GUILayout.Label("1  –  Ability 1 (Cleave)", _labelStyle);
            GUILayout.Label("2  –  Ability 2", _labelStyle);
            GUILayout.Label("B  –  Toggle Build Mode", _labelStyle);
            GUILayout.Label("F1  –  Screen Logger", _labelStyle);
            GUILayout.Label("F2  –  Toggle This HUD", _labelStyle);
            GUILayout.Label("Shift+Tab (hold)  –  Asset Deploy UI", _labelStyle);

            // Build mode section
            if (_buildingController != null && _buildingController.IsBuildModeActive)
            {
                GUILayout.Space(8);
                GUILayout.Label("BUILD MODE", _headerStyle);
                GUILayout.Space(4);
                GUILayout.Label("1-9  –  Select Recipe", _labelStyle);
                GUILayout.Label("Scroll  –  Rotate Piece", _labelStyle);
                GUILayout.Label("Shift+Scroll  –  Tilt/Pitch", _labelStyle);
                GUILayout.Label("Left Click  –  Place", _labelStyle);
                GUILayout.Label("Middle Click  –  Remove", _labelStyle);
                GUILayout.Label("Right Click  –  Repair", _labelStyle);

                // Show recipes with active highlight
                var recipes = _buildingController.Recipes;
                if (recipes != null && recipes.Count > 0)
                {
                    GUILayout.Space(6);
                    GUILayout.Label("RECIPES:", _headerStyle);
                    for (int i = 0; i < recipes.Count; i++)
                    {
                        bool isSelected = i == _buildingController.SelectedRecipeIndex;
                        var style = isSelected ? _activeStyle : _labelStyle;
                        string marker = isSelected ? " <<" : "";
                        GUILayout.Label($"  [{i + 1}] {recipes[i].RecipeName}{marker}", style);
                    }
                }
            }

            bool devMode = GameConstants.IsDevMode();
            if (devMode)
            {
                GUILayout.Space(6);
                GUILayout.Label("DEV MODE: ON (free building)", _activeStyle);
            }

            GUILayout.EndArea();
        }

        private void InitStyles()
        {
            if (_bgStyle != null) return;

            _bgStyle = new GUIStyle(GUI.skin.box);
            _bgStyle.normal.background = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.6f));

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.3f) }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = Color.white }
            };

            _activeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.3f, 1f, 0.3f) }
            };
        }

        private static Texture2D MakeTex(int width, int height, Color color)
        {
            var pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = color;
            var tex = new Texture2D(width, height);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }
    }
}
