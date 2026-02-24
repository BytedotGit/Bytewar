using UnityEngine;
using System.Collections.Generic;

namespace ByteWar.UI
{
    /// <summary>
    /// In-game developer console. Toggle with backtick/tilde (~) key.
    /// Accepts slash commands:
    ///   /dev   — toggle DevMode on/off
    ///   /help  — list available commands
    /// When open, suppresses gameplay input via PlayerInputHandler.InputSuppressed.
    /// Uses IMGUI for rendering (matches ScreenLogger / KeybindingHUD pattern).
    /// All keyboard handling uses IMGUI Event.current in OnGUI() — this is immune
    /// to Unity Input System focus issues that can make Keyboard.current silent.
    /// </summary>
    public class DevConsole : MonoBehaviour
    {
        private bool _isOpen;
        private string _inputText = "";
        private readonly List<ConsoleLine> _outputLines = new List<ConsoleLine>();
        private Vector2 _scrollPos;
        private const int MaxOutputLines = 50;
        private const string InputControlName = "DevConsoleInput";

        private GUIStyle _outputStyle;
        private GUIStyle _inputStyle;
        private bool _focusNextFrame;
        private bool _pendingClose;

        private Core.PlayerInputHandler _cachedInputHandler;

        private struct ConsoleLine
        {
            public string Text;
            public Color Color;
        }

        private void OnGUI()
        {
            // ── Backtick toggles console open/closed ─────────────────────────
            // Handled BEFORE the _isOpen guard so it works in both states.
            // Using IMGUI Event.current instead of Keyboard.current because the
            // Input System can silently stop reporting key presses in certain
            // focus states (e.g., after IMGUI TextField takes focus).
            if (Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.BackQuote)
            {
                _isOpen = !_isOpen;
                if (_isOpen)
                {
                    _focusNextFrame = true;
                    _pendingClose = false;
                }
                else
                {
                    CloseConsole();
                }
                UpdateInputSuppression();
                Event.current.Use();
                Debug.Log($"[DevConsole] Console {(_isOpen ? "opened" : "closed")} via BackQuote.");
                return;
            }

            if (!_isOpen) return;

            InitStyles();

            float panelW = Screen.width;
            float panelH = Screen.height * 0.3f;
            float inputH = 28f;
            float y = Screen.height - panelH;

            // Dark background
            GUI.Box(new Rect(0, y, panelW, panelH), "");
            GUI.Box(new Rect(0, y, panelW, panelH), ""); // Double-draw for darker

            // Output area
            Rect outputArea = new Rect(4, y + 4, panelW - 8, panelH - inputH - 12);
            GUILayout.BeginArea(outputArea);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);
            foreach (var line in _outputLines)
            {
                GUI.color = line.Color;
                GUILayout.Label(line.Text, _outputStyle);
            }
            GUI.color = Color.white;
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Input field
            Rect inputRect = new Rect(4, y + panelH - inputH - 4, panelW - 8, inputH);

            // Handle Escape and Enter/Return via Event.current (reliable when
            // IMGUI TextField has focus; Keyboard.current is not).
            bool submit = false;
            if (Event.current.type == EventType.KeyDown)
            {
                switch (Event.current.keyCode)
                {
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        if (GUI.GetNameOfFocusedControl() == InputControlName)
                        {
                            submit = true;
                            Event.current.Use();
                        }
                        break;

                    case KeyCode.Escape:
                        _isOpen = false;
                        CloseConsole();
                        UpdateInputSuppression();
                        Event.current.Use();
                        Debug.Log("[DevConsole] Console closed via Escape.");
                        return;
                }
            }

            GUI.SetNextControlName(InputControlName);
            _inputText = GUI.TextField(inputRect, _inputText, _inputStyle);

            if (_focusNextFrame)
            {
                GUI.FocusControl(InputControlName);
                _focusNextFrame = false;
            }

            if (submit && !string.IsNullOrWhiteSpace(_inputText))
            {
                ProcessCommand(_inputText.Trim());
                _inputText = "";
                _focusNextFrame = true;
            }

            // Deferred close (e.g. after /dev command) — must happen after GUI
            // controls are drawn to avoid IMGUI layout mismatches.
            if (_pendingClose)
            {
                _pendingClose = false;
                _isOpen = false;
                CloseConsole();
                UpdateInputSuppression();
                Debug.Log("[DevConsole] Console auto-closed after command.");
            }
        }

        private void ProcessCommand(string raw)
        {
            AddOutput($"> {raw}", Color.white);
            Debug.Log($"[DevConsole] Command: {raw}");

            string cmd = raw.ToLowerInvariant();

            switch (cmd)
            {
                case "/dev":
                    {
                        bool current = Core.GameConstants.IsDevMode();
                        Core.GameConstants.SetDevMode(!current);
                        string state = !current ? "ON" : "OFF";
                        AddOutput($"DevMode: {state}", Color.green);
                        // Auto-close console so input is restored immediately
                        _pendingClose = true;
                        break;
                    }
                case "/help":
                    AddOutput("Available commands:", Color.cyan);
                    AddOutput("  /dev   - Toggle developer mode (free resources, debug tools)", Color.cyan);
                    AddOutput("  /help  - Show this help message", Color.cyan);
                    break;
                default:
                    AddOutput($"Unknown command: {raw}. Type /help for a list.", Color.yellow);
                    break;
            }
        }

        private void AddOutput(string text, Color color)
        {
            _outputLines.Add(new ConsoleLine { Text = text, Color = color });
            if (_outputLines.Count > MaxOutputLines)
                _outputLines.RemoveAt(0);
            _scrollPos = new Vector2(0, float.MaxValue); // auto-scroll to bottom
        }

        private void CloseConsole()
        {
            _pendingClose = false;
            _inputText = "";
            GUIUtility.keyboardControl = 0;
            GUI.FocusControl(null);

            // Force-reset InputActions so the Input System recovers from IMGUI
            // TextField stealing keyboard focus (known Unity Input System issue).
            if (_cachedInputHandler != null)
            {
                _cachedInputHandler.ForceResetActions();
            }
        }

        private void UpdateInputSuppression()
        {
            if (_cachedInputHandler == null)
            {
                // Find any PlayerInputHandler in the scene
                _cachedInputHandler = FindFirstObjectByType<Core.PlayerInputHandler>();
            }

            if (_cachedInputHandler != null)
            {
                _cachedInputHandler.InputSuppressed = _isOpen;
            }
        }

        private void OnDisable()
        {
            // Ensure input is restored when console is destroyed
            if (_cachedInputHandler != null)
            {
                _cachedInputHandler.InputSuppressed = false;
            }
        }

        private void InitStyles()
        {
            if (_outputStyle != null) return;

            _outputStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                richText = false,
                padding = new RectOffset(2, 2, 1, 1),
            };

            _inputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 16,
            };
        }

        /// <summary>Exposed for testing: process a command string directly.</summary>
        internal void ExecuteCommand(string command)
        {
            ProcessCommand(command);
        }

        /// <summary>Exposed for testing: open or close the console programmatically.</summary>
        internal bool IsOpen
        {
            get => _isOpen;
            set
            {
                _isOpen = value;
                if (!_isOpen) _inputText = "";
                UpdateInputSuppression();
            }
        }

        /// <summary>Exposed for testing: returns the current output lines count.</summary>
        internal int OutputLineCount => _outputLines.Count;

        /// <summary>Exposed for testing: returns the last output line text.</summary>
        internal string LastOutputText => _outputLines.Count > 0 ? _outputLines[_outputLines.Count - 1].Text : null;
    }
}
