using System.Collections;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using SurvivalRPG.Abilities;
using SurvivalRPG.Survival;
using SurvivalRPG.Networking;
using SurvivalRPG.Core;

namespace SurvivalRPG.Tests.PlayMode
{
    public static class NGOTestHelper
    {
        public static NetworkManager CreateNetworkManager()
        {
            var go = new GameObject("NetworkManager");
            var networkManager = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();

            // In a real multi-process test, we would use MultiInstanceHelpers to spin up a separate Client.
            // For these smoke tests, StartHost() provides both Server and Client functionality in one instance.
            networkManager.NetworkConfig = new Unity.Netcode.NetworkConfig()
            {
                NetworkTransport = transport,
                PlayerPrefab = CreatePlayerPrefab()
            };

            return networkManager;
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
            go.AddComponent<PlayerInputHandler>();
            go.AddComponent<PlayerInteraction>();
            go.AddComponent<NetworkPlayer>();

            return go;
        }

        public static void CleanUp()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
                Object.Destroy(NetworkManager.Singleton.gameObject);
            }
        }
    }
}