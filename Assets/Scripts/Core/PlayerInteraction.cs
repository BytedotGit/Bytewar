using UnityEngine;
using Unity.Netcode;
using SurvivalRPG.Survival;
using SurvivalRPG.Abilities;
using SurvivalRPG.UI;

namespace SurvivalRPG.Core
{
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerInteraction : NetworkBehaviour
    {
        private PlayerInputHandler _inputHandler;
        private Camera _mainCamera;
        private AbilitySystemComponent _abilitySystem;
        private Animator _animator;

        private void Awake()
        {
            _inputHandler = GetComponent<PlayerInputHandler>();
            _abilitySystem = GetComponent<AbilitySystemComponent>();
            _animator = GetComponent<Animator>();
            _mainCamera = Camera.main;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                // Disable input handler if not owner
                if (_inputHandler != null)
                {
                    _inputHandler.enabled = false;
                }
            }
            else
            {
                Debug.Log($"[{nameof(PlayerInteraction)}] Initialized for local player (ClientId: {NetworkManager.Singleton.LocalClientId}).");

                if (_inputHandler != null)
                {
                    if (!_inputHandler.enabled)
                    {
                        Debug.LogWarning($"[{nameof(PlayerInteraction)}] PlayerInputHandler was disabled on owner — re-enabling.");
                        _inputHandler.enabled = true;
                    }
                    _inputHandler.EnsureActionsEnabled($"{nameof(PlayerInteraction)} OnNetworkSpawn (owner)");
                }
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner) return;

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            HandleInteraction();
        }

        private void HandleInteraction()
        {
            if (_inputHandler.ConsumeInteract())
            {
                Debug.Log($"[{nameof(PlayerInteraction)}] Interact action triggered.");
                PerformRaycastInteraction();
            }
        }

        private void PerformRaycastInteraction()
        {
            if (UnityEngine.InputSystem.Mouse.current == null)
            {
                Debug.LogWarning($"[{nameof(PlayerInteraction)}] Mouse not found.");
                return;
            }

            Ray ray = _mainCamera.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                Debug.Log($"[{nameof(PlayerInteraction)}] Raycast hit: {hit.collider.gameObject.name}");

                ResourceNode resourceNode = hit.collider.GetComponent<ResourceNode>();
                if (resourceNode != null)
                {
                    Debug.Log($"[{nameof(PlayerInteraction)}] Hit ResourceNode: {resourceNode.gameObject.name}. Attempting to gather.");
                    if (_animator != null) _animator.SetTrigger("Gather");
                    GatherResourceServerRpc(resourceNode.NetworkObjectId);
                    return;
                }

                EnemyAI enemyAI = hit.collider.GetComponent<EnemyAI>();
                if (enemyAI != null)
                {
                    Debug.Log($"[{nameof(PlayerInteraction)}] Hit EnemyAI: {enemyAI.gameObject.name} (NetworkObjectId={enemyAI.NetworkObjectId}). Attacking.");
                    if (_animator != null) _animator.SetTrigger("Attack");
                    EnemyTargetTracker.SetTarget(enemyAI);
                    AttackEnemyServerRpc(enemyAI.NetworkObjectId);
                    return;
                }
            }
            else
            {
                Debug.Log($"[{nameof(PlayerInteraction)}] Raycast hit nothing.");
            }
        }

        private Vector3 GetMouseWorldPosition()
        {
            if (UnityEngine.InputSystem.Mouse.current == null) return Vector3.zero;

            Ray ray = _mainCamera.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                return hit.point;
            }
            return Vector3.zero;
        }

        [ServerRpc]
        private void AttackEnemyServerRpc(ulong enemyNetworkObjectId, ServerRpcParams rpcParams = default)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyNetworkObjectId, out NetworkObject netObj))
            {
                EnemyAI enemy = netObj.GetComponent<EnemyAI>();
                if (enemy != null)
                {
                    Debug.Log($"[{nameof(PlayerInteraction)}] Server: ClientId {rpcParams.Receive.SenderClientId} melee-attacking '{enemy.gameObject.name}'.");
                    enemy.TakeDamage(10f);
                }
            }
            else
            {
                Debug.LogWarning($"[{nameof(PlayerInteraction)}] Server: EnemyAI NetworkObjectId {enemyNetworkObjectId} not found.");
            }
        }

        [ServerRpc]
        private void GatherResourceServerRpc(ulong resourceNetworkObjectId, ServerRpcParams rpcParams = default)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(resourceNetworkObjectId, out NetworkObject networkObject))
            {
                ResourceNode resourceNode = networkObject.GetComponent<ResourceNode>();
                if (resourceNode != null)
                {
                    Debug.Log($"[{nameof(PlayerInteraction)}] Server processing gather request from ClientId: {rpcParams.Receive.SenderClientId} for {resourceNode.gameObject.name}.");
                    // Assuming 10 damage per gather for now
                    InventoryComponent gatherer = GetComponent<InventoryComponent>();
                    resourceNode.TakeDamage(10f, gatherer);
                }
            }
            else
            {
                Debug.LogWarning($"[{nameof(PlayerInteraction)}] Server could not find ResourceNode with NetworkObjectId: {resourceNetworkObjectId}.");
            }
        }
    }
}
