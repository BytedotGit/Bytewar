using UnityEngine;
using System.Collections.Generic;

namespace ByteWar.Core
{
    /// <summary>
    /// Captures Debug.Log / LogWarning / LogError and renders the last N messages
    /// on screen using IMGUI, so runtime issues are visible in builds.
    /// Toggle with F1.
    /// </summary>
    public class ScreenLogger : MonoBehaviour
    {
        private struct Entry
        {
            public string message;
            public LogType type;
        }

        private const int MaxEntries = 30;
        private readonly List<Entry> _entries = new List<Entry>();
        private bool _visible = false;  // Off by default; press F1 to toggle
        private Vector2 _scroll;
        private GUIStyle _logStyle;

        private void OnEnable()
        {
            Application.logMessageReceived += OnLogMessage;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            _entries.Add(new Entry { message = condition, type = type });
            if (_entries.Count > MaxEntries)
                _entries.RemoveAt(0);
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame)
            {
                _visible = !_visible;
            }
        }

        private void OnGUI()
        {
            if (!_visible || _entries.Count == 0) return;

            if (_logStyle == null)
            {
                _logStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    wordWrap = true,
                    richText = false
                };
            }

            float w = Screen.width * 0.45f;
            float h = Screen.height * 0.45f;
            Rect area = new Rect(Screen.width - w - 10, Screen.height - h - 10, w, h);

            GUI.Box(area, "");
            GUILayout.BeginArea(area);
            _scroll = GUILayout.BeginScrollView(_scroll);

            foreach (var e in _entries)
            {
                switch (e.type)
                {
                    case LogType.Error:
                    case LogType.Exception:
                        GUI.color = Color.red;
                        break;
                    case LogType.Warning:
                        GUI.color = Color.yellow;
                        break;
                    default:
                        GUI.color = Color.white;
                        break;
                }
                GUILayout.Label(e.message, _logStyle);
            }
            GUI.color = Color.white;

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
