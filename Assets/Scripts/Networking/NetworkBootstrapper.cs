using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace ByteWar.Networking
{
    /// <summary>
    /// Validates and self-repairs the NetworkManager configuration at runtime,
    /// before any StartHost / StartClient / StartServer call is made.
    /// Attach this to the same GameObject as NetworkManager.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class NetworkBootstrapper : MonoBehaviour
    {
        // Set by PrefabGenerator at prefab-bake time as a serialized fallback.
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private UnityTransport _transport;

        private void Awake()
        {
            Validate();
        }

        public void Validate()
        {
            var nm = GetComponent<NetworkManager>();
            if (nm == null)
            {
                Debug.LogError("[NetworkBootstrapper] No NetworkManager on this GameObject!");
                return;
            }

            // ── Transport ─────────────────────────────────────────────────────────
            if (nm.NetworkConfig.NetworkTransport == null)
            {
                var transport = _transport != null ? _transport : GetComponent<UnityTransport>();
                if (transport == null)
                    transport = gameObject.AddComponent<UnityTransport>();

                nm.NetworkConfig.NetworkTransport = transport;
                Debug.LogWarning($"[NetworkBootstrapper] Repaired null NetworkTransport → {transport.GetType().Name}");
            }

            // ── Player Prefab ─────────────────────────────────────────────────────
            if (nm.NetworkConfig.PlayerPrefab == null)
            {
                if (_playerPrefab != null)
                {
                    nm.NetworkConfig.PlayerPrefab = _playerPrefab;
                    Debug.LogWarning($"[NetworkBootstrapper] Repaired null PlayerPrefab → {_playerPrefab.name}");
                }
                else
                {
                    Debug.LogError("[NetworkBootstrapper] PlayerPrefab is null and no fallback set! Players won't spawn.");
                }
            }

            Debug.Log($"[NetworkBootstrapper] Config OK. Transport={nm.NetworkConfig.NetworkTransport?.GetType().Name ?? "NULL"} | PlayerPrefab={nm.NetworkConfig.PlayerPrefab?.name ?? "NULL"}");
        }
    }
}
