using UnityEngine;
using Unity.Netcode;

namespace ByteWar.Core
{
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Fix for D3D12 VSync time drift bug that freezes Time.deltaTime
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Debug.Log("Server started. Waiting for players...");
            }
        }
    }
}
