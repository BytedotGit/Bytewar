using System.Collections;
using NUnit.Framework;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using ByteWar.Abilities;
using ByteWar.Survival;
using ByteWar.Networking;
using ByteWar.Core;
using ByteWar.Building;

namespace ByteWar.Tests.PlayMode
{
    public static class NGOTestHelper
    {
        public static NetworkManager CreateNetworkManager()
        {
            var go = new GameObject("NetworkManager");
            var networkManager = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();

            // Avoid flaky transport start failures when multiple PlayMode tests run in-process.
            // We pick a free UDP port per test NetworkManager instance.
            ushort port = ChooseEphemeralUdpPort();
            transport.SetConnectionData("127.0.0.1", port, transport.ConnectionData.ServerListenAddress);
            Debug.Log($"[NGOTestHelper] Using UnityTransport port {port} for PlayMode test NetworkManager.");

            var playerPrefab = CreatePlayerPrefab();

            // In a real multi-process test, we would use MultiInstanceHelpers to spin up a separate Client.
            // For these smoke tests, StartHost() provides both Server and Client functionality in one instance.
            networkManager.NetworkConfig = new Unity.Netcode.NetworkConfig()
            {
                NetworkTransport = transport,
                PlayerPrefab = playerPrefab
            };

            return networkManager;
        }

        private static ushort ChooseEphemeralUdpPort()
        {
            // Bind to port 0 to let the OS select a free ephemeral port.
            // We dispose immediately; there is a small race, but this is good enough for test isolation.
            using var client = new UdpClient(0);
            return (ushort)((IPEndPoint)client.Client.LocalEndPoint).Port;
        }

        public static IEnumerator WaitForLocalPlayerReady(NetworkManager networkManager, float timeoutSeconds = 10f)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < timeoutSeconds)
            {
                if (networkManager != null && networkManager.IsListening && networkManager.SpawnManager != null)
                {
                    NetworkObject playerNetObj = networkManager.SpawnManager.GetLocalPlayerObject();
                    if (playerNetObj != null)
                    {
                        if (playerNetObj.IsSpawned)
                        {
                            // Some PlayMode test setups use dynamically created PlayerPrefabs. Depending on NGO version,
                            // the auto-spawned player can end up without fully spawned NetworkBehaviours, which breaks
                            // RPC-based gameplay tests. If that happens, replace the player with a scene-spawned test player.
                            if (!IsPlayerUsableForTests(playerNetObj))
                            {
                                Debug.LogWarning("[NGOTestHelper] Local player exists but is not usable for tests (missing/unspawned NetworkBehaviours). Replacing with test player...");
                                ReplaceLocalPlayerWithTestPlayer(networkManager, playerNetObj);
                                // Give NGO a frame to process the replacement.
                                yield return null;
                            }

                            // Give NGO a frame for OnNetworkSpawn initialization to run.
                            yield return null;
                            yield break;
                        }
                    }
                }

                yield return null;
            }

            NetworkObject playerAtTimeout = null;
            bool netObjSpawnedAtTimeout = false;
            if (networkManager != null && networkManager.SpawnManager != null)
            {
                playerAtTimeout = networkManager.SpawnManager.GetLocalPlayerObject();
                netObjSpawnedAtTimeout = playerAtTimeout != null && playerAtTimeout.IsSpawned;
            }

            string playerName = playerAtTimeout != null && playerAtTimeout.gameObject != null ? playerAtTimeout.gameObject.name : "(null)";
            Assert.Fail($"[NGOTestHelper] Timed out waiting for local player spawn/ready. IsListening={networkManager != null && networkManager.IsListening} IsHost={networkManager != null && networkManager.IsHost} LocalPlayerObj={playerName} NetObjSpawned={netObjSpawnedAtTimeout}");
        }

        private static bool IsPlayerUsableForTests(NetworkObject playerNetObj)
        {
            if (playerNetObj == null) return false;
            if (!playerNetObj.IsSpawned) return false;

            var playerGo = playerNetObj.gameObject;
            if (playerGo == null) return false;

            // These components are used across our PlayMode smoke tests.
            var asc = playerGo.GetComponent<AbilitySystemComponent>();
            var inv = playerGo.GetComponent<InventoryComponent>();
            var eq = playerGo.GetComponent<EquipmentComponent>();
            var np = playerGo.GetComponent<NetworkPlayer>();

            if (asc == null || inv == null || eq == null || np == null) return false;

            // Crucially: RPC-based tests require the NetworkBehaviours to be spawned.
            if (!asc.IsSpawned) return false;
            if (!inv.IsSpawned) return false;
            if (!eq.IsSpawned) return false;
            if (!np.IsSpawned) return false;

            return true;
        }

        private static void ReplaceLocalPlayerWithTestPlayer(NetworkManager networkManager, NetworkObject oldPlayer)
        {
            if (networkManager == null) return;
            if (!networkManager.IsServer)
            {
                Debug.LogError("[NGOTestHelper] ReplaceLocalPlayerWithTestPlayer called while not server.");
                return;
            }

            // Many runtime components assume a Main Camera exists (e.g., NetworkPlayer camera setup).
            // PlayMode tests run in an empty scene by default, so create a minimal Main Camera to avoid LogError failures.
            if (Camera.main == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                camGo.AddComponent<Camera>();
            }

            ulong clientId = networkManager.LocalClientId;

            if (oldPlayer != null && oldPlayer.IsSpawned)
            {
                oldPlayer.Despawn(true);
            }

            var playerGo = new GameObject("TestPlayer");
            var netObj = playerGo.AddComponent<NetworkObject>();

            // Core systems
            playerGo.AddComponent<AttributeSet>();
            playerGo.AddComponent<AbilitySystemComponent>();
            playerGo.AddComponent<SurvivalStats>();
            playerGo.AddComponent<InventoryComponent>();
            playerGo.AddComponent<EquipmentComponent>();
            playerGo.AddComponent<PlayerInputHandler>();
            playerGo.AddComponent<PlayerInteraction>();
            playerGo.AddComponent<NetworkPlayer>();

            // Spawn and mark as the client's player object.
            netObj.SpawnAsPlayerObject(clientId, destroyWithScene: true);
        }

        public static GameObject CreatePlayerPrefab()
        {
            var go = new GameObject("PlayerPrefab");
            go.SetActive(false); // Must be inactive to act as a prefab

            go.AddComponent<NetworkObject>();
            go.AddComponent<AttributeSet>();
            go.AddComponent<AbilitySystemComponent>();
            go.AddComponent<SurvivalStats>();
            go.AddComponent<InventoryComponent>();
            go.AddComponent<EquipmentComponent>();
            go.AddComponent<PlayerInputHandler>();
            go.AddComponent<PlayerInteraction>();
            go.AddComponent<NetworkPlayer>();

            return go;
        }

        public static void CleanUp()
        {
            if (NetworkManager.Singleton != null)
            {
                // Destroy the dynamically created player prefab as well to prevent cross-test leakage.
                var playerPrefab = NetworkManager.Singleton.NetworkConfig != null ? NetworkManager.Singleton.NetworkConfig.PlayerPrefab : null;
                if (playerPrefab != null)
                {
                    Object.Destroy(playerPrefab);
                }

                NetworkManager.Singleton.Shutdown();
                Object.Destroy(NetworkManager.Singleton.gameObject);
            }
        }
    }
}