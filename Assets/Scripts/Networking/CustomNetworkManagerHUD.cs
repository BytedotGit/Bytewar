using System;
using UnityEngine;
using Unity.Netcode;
using ByteWar.Core;

namespace ByteWar.Networking
{
    /// <summary>
    /// Network HUD using IMGUI. Requires activeInputHandler set to "Both" (2)
    /// so that legacy Input and GUILayout.Button work in builds.
    /// </summary>
    public class CustomNetworkManagerHUD : MonoBehaviour
    {
        private const string NoAutoHostArg = "-noAutoHost";
        private const string AutoTestArg = "-autoTest";

        private GUIStyle _buttonStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _errorStyle;
        private string _statusMsg = "";
        private Color _statusColor = Color.white;

        private void Start()
        {
            if (NetworkManager.Singleton == null)
            {
                SetStatus("ERROR: NetworkManager.Singleton is NULL!", Color.red);
                Debug.LogError("[HUD] NetworkManager.Singleton is NULL!");
                return;
            }

            var boot = GetComponent<NetworkBootstrapper>();
            if (boot != null) boot.Validate();

            var config = NetworkManager.Singleton.NetworkConfig;
            string tName = config.NetworkTransport != null ? config.NetworkTransport.GetType().Name : "NULL";
            string pName = config.PlayerPrefab != null ? config.PlayerPrefab.name : "NULL";
            Debug.Log($"[HUD] Start — Transport: {tName} | PlayerPrefab: {pName}");
            SetStatus($"Ready. Transport={tName} Player={pName}", Color.green);

            string[] args = Environment.GetCommandLineArgs();
            bool shouldAutoHost = ShouldAutoHost(
                isEditor: Application.isEditor,
                isServer: NetworkManager.Singleton.IsServer,
                isClient: NetworkManager.Singleton.IsClient,
                args: args);

            if (!shouldAutoHost)
            {
                Debug.Log($"[HUD] Auto-host skipped. editor={Application.isEditor} server={NetworkManager.Singleton.IsServer} client={NetworkManager.Singleton.IsClient} hasNoAutoHostArg={HasArg(args, NoAutoHostArg)} isAutoTest={HasArg(args, AutoTestArg)}");
                return;
            }

            // Standalone UX: spawn a local player automatically so movement/camera work immediately.
            Debug.Log("[HUD] Auto-hosting for standalone build...");
            SetStatus("Auto-hosting...", Color.yellow);

            try
            {
                bool ok = NetworkManager.Singleton.StartHost();
                Debug.Log($"[HUD] StartHost() returned {ok} (auto-host)");
                SetStatus(ok ? "HOST started!" : "ERROR: Auto-host StartHost() returned false", ok ? Color.green : Color.red);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HUD] Auto-host StartHost exception: {ex}");
                SetStatus($"EXCEPTION: {ex.Message}", Color.red);
            }
        }

        public static bool ShouldAutoHost(bool isEditor, bool isServer, bool isClient, string[] args)
        {
            if (isEditor) return false;
            if (isServer || isClient) return false;
            if (HasArg(args, NoAutoHostArg)) return false;
            if (HasArg(args, AutoTestArg)) return false;
            return true;
        }

        private static bool HasArg(string[] args, string arg)
        {
            if (args == null || args.Length == 0) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], arg, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void SetStatus(string msg, Color color)
        {
            _statusMsg = msg;
            _statusColor = color;
        }

        private void InitStyles()
        {
            if (_buttonStyle != null) return;
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                fixedWidth = 260,
                fixedHeight = 55
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            _errorStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = Color.red }
            };
        }

        private void OnGUI()
        {
            InitStyles();

            if (NetworkManager.Singleton == null)
            {
                GUILayout.BeginArea(new Rect(10, 10, 400, 80));
                GUI.color = Color.red;
                GUILayout.Label("ERROR: NetworkManager not found!", _labelStyle);
                GUI.color = Color.white;
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 400, 550));
            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
                DrawStartButtons();
            else
                DrawStatusLabels();
            GUILayout.EndArea();
        }

        private void DrawStartButtons()
        {
            var config = NetworkManager.Singleton.NetworkConfig;
            bool hasTransport = config.NetworkTransport != null;
            bool hasPrefab = config.PlayerPrefab != null;

            // Diagnostics (DevMode only)
            if (GameConstants.IsDevMode())
            {
                GUI.color = hasTransport ? Color.green : Color.red;
                GUILayout.Label($"Transport: {(hasTransport ? config.NetworkTransport.GetType().Name : "MISSING")}", _labelStyle);
                GUI.color = hasPrefab ? Color.green : Color.red;
                GUILayout.Label($"PlayerPrefab: {(hasPrefab ? config.PlayerPrefab.name : "MISSING")}", _labelStyle);
                GUI.color = Color.white;

                // Status message
                if (_statusMsg.Length > 0)
                {
                    GUI.color = _statusColor;
                    GUILayout.Label(_statusMsg, _statusMsg.Contains("ERROR") ? _errorStyle : _labelStyle);
                    GUI.color = Color.white;
                }
            }

            GUILayout.Space(10);

            // ── Host Button ──
            if (GUILayout.Button("HOST  (Start Game)", _buttonStyle))
            {
                Debug.Log("[HUD] HOST button pressed.");
                SetStatus("Starting host...", Color.yellow);
                try
                {
                    bool ok = NetworkManager.Singleton.StartHost();
                    Debug.Log($"[HUD] StartHost() returned {ok}");
                    if (ok)
                        SetStatus("HOST started!", Color.green);
                    else
                        SetStatus("ERROR: StartHost() returned false", Color.red);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[HUD] StartHost exception: {ex}");
                    SetStatus($"EXCEPTION: {ex.Message}", Color.red);
                }
            }

            GUILayout.Space(4);
            if (GUILayout.Button("CLIENT  (Join)", _buttonStyle))
            {
                Debug.Log("[HUD] CLIENT button pressed.");
                try
                {
                    bool ok = NetworkManager.Singleton.StartClient();
                    if (!ok) SetStatus("ERROR: StartClient() returned false", Color.red);
                }
                catch (System.Exception ex)
                {
                    SetStatus($"EXCEPTION: {ex.Message}", Color.red);
                }
            }

            GUILayout.Space(4);
            if (GUILayout.Button("SERVER  (Dedicated)", _buttonStyle))
            {
                Debug.Log("[HUD] SERVER button pressed.");
                try
                {
                    bool ok = NetworkManager.Singleton.StartServer();
                    if (!ok) SetStatus("ERROR: StartServer() returned false", Color.red);
                }
                catch (System.Exception ex)
                {
                    SetStatus($"EXCEPTION: {ex.Message}", Color.red);
                }
            }

            if (GameConstants.IsDevMode())
            {
                GUILayout.Space(10);
                GUI.color = new Color(1, 1, 1, 0.6f);
                GUILayout.Label("RMB drag = orbit  |  Scroll = zoom", _labelStyle);
                GUILayout.Label("WASD = move  |  ESC = show cursor", _labelStyle);
                GUI.color = Color.white;
            }
        }

        private void DrawStatusLabels()
        {
            var nm = NetworkManager.Singleton;
            string mode = nm.IsHost ? "Host" : nm.IsServer ? "Server" : "Client";

            GUI.color = Color.green;
            GUILayout.Label($"STATUS: {mode.ToUpper()} RUNNING", _labelStyle);
            GUI.color = Color.white;
            GUILayout.Label($"Connected clients: {nm.ConnectedClients.Count}", _labelStyle);
            GUILayout.Space(8);

            if (GUILayout.Button("Disconnect", _buttonStyle))
            {
                nm.Shutdown();
                Debug.Log("[HUD] Shutdown called.");
                SetStatus("Disconnected.", Color.yellow);
            }

            if (GameConstants.IsDevMode())
            {
                GUILayout.Space(10);
                GUI.color = new Color(1, 1, 1, 0.6f);
                GUILayout.Label("RMB drag = orbit  |  Scroll = zoom", _labelStyle);
                GUILayout.Label("WASD = move  |  ESC = show cursor", _labelStyle);
                GUI.color = Color.white;
            }
        }
    }
}
