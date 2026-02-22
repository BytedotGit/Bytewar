using UnityEngine;
using Unity.Netcode;

namespace ByteWar.Core
{
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameStateMachine StateMachine { get; private set; }

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
            Application.targetFrameRate = GameConstants.GetTargetFrameRate();
            QualitySettings.vSyncCount = 0;

            StateMachine = new GameStateMachine();
            StateMachine.OnStateChanged += (prev, next) =>
            {
                GameEventBus.GameStateChanged.Raise(new GameStateChangedEvent
                {
                    Previous = prev,
                    Current = next,
                });
            };
            StateMachine.TransitionTo(GameStateType.Initializing);
            Debug.Log("[GameManager] GameStateMachine initialized.");
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Debug.Log("Server started. Waiting for players...");
                StateMachine.TransitionTo(GameStateType.Playing);
            }
        }

        private void Update()
        {
            StateMachine?.Tick();
        }
    }
}
