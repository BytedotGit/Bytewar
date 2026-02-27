using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ByteWar.Survival;
using ByteWar.Abilities;
using ByteWar.UI;

namespace ByteWar.Core
{
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerInteraction : NetworkBehaviour
    {
        private PlayerInputHandler _inputHandler;
        private Camera _mainCamera;
        private AbilitySystemComponent _abilitySystem;
        private EquipmentComponent _equipment;
        private Animator _animator;
        [SerializeField] private float _destructibleRequestCooldownSeconds = 0.1f;
        [SerializeField] private float _destructibleServerValidationRangePadding = 1f;

        private readonly Dictionary<ulong, float> _lastDestructibleRequestTimes = new();

        private void Awake()
        {
            _inputHandler = GetComponent<PlayerInputHandler>();
            _abilitySystem = GetComponent<AbilitySystemComponent>();
            _equipment = GetComponent<EquipmentComponent>();
            _animator = GetComponent<Animator>();
            _mainCamera = Camera.main;
        }

        public override void OnNetworkDespawn()
        {
            _lastDestructibleRequestTimes.Clear();
            base.OnNetworkDespawn();
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
            if (Physics.Raycast(ray, out RaycastHit hit, GameConstants.GetInteractionRange()))
            {
                Debug.Log($"[{nameof(PlayerInteraction)}] Raycast hit: {hit.collider.gameObject.name}");

                // Try IDamageable + ICombatTarget (enemy combat)
                var combatTarget = hit.collider.GetComponent<ICombatTarget>();
                if (combatTarget != null && combatTarget.IsAlive)
                {
                    Debug.Log($"[{nameof(PlayerInteraction)}] Hit combat target: {hit.collider.gameObject.name}. Attacking.");
                    if (_animator != null) _animator.SetTrigger("Attack");
                    var enemyAI = hit.collider.GetComponent<EnemyAI>();
                    if (enemyAI != null)
                    {
                        EnemyTargetTracker.SetTarget(enemyAI);
                        AttackEnemyServerRpc(enemyAI.NetworkObjectId);
                    }
                    return;
                }

                var destructible = hit.collider.GetComponentInParent<DestructibleComponent>();
                if (destructible != null && destructible.IsAlive)
                {
                    Debug.Log($"[{nameof(PlayerInteraction)}] Hit destructible '{destructible.gameObject.name}'. Requesting server damage.");
                    if (_animator != null) _animator.SetTrigger("Gather");
                    TryDamageDestructible(destructible);
                    return;
                }

                // Try IInteractable (resource gathering, crafting stations, etc.)
                var interactable = hit.collider.GetComponent<IInteractable>();
                if (interactable != null && interactable.CanInteract(OwnerClientId))
                {
                    Debug.Log($"[{nameof(PlayerInteraction)}] Hit interactable: {interactable.InteractionName}. Interacting.");
                    if (_animator != null) _animator.SetTrigger("Gather");
                    // ResourceNode still needs the old path for gatherer param
                    var resourceNode = hit.collider.GetComponent<ResourceNode>();
                    if (resourceNode != null)
                    {
                        GatherResourceServerRpc(resourceNode.NetworkObjectId);
                    }
                    return;
                }
            }
            else
            {
                Debug.Log($"[{nameof(PlayerInteraction)}] Raycast hit nothing.");
            }
        }

        public void TryDamageDestructible(DestructibleComponent destructible)
        {
            if (!IsSpawned || !IsOwner)
            {
                Debug.LogWarning($"[{nameof(PlayerInteraction)}] Rejecting destructible request because caller is not spawned owner.");
                return;
            }

            if (destructible == null || !destructible.IsSpawned)
            {
                Debug.LogWarning($"[{nameof(PlayerInteraction)}] Cannot damage destructible: target is null or not spawned.");
                return;
            }

            DamageType damageType = ResolveCurrentDamageType();
            float baseDamage = ResolveCurrentToolDamage();
            Debug.Log($"[{nameof(PlayerInteraction)}] Requesting destructible damage netObj={destructible.NetworkObjectId} type={damageType} baseDamage={baseDamage:0.##}");
            RequestDestructibleDamageServerRpc(destructible.NetworkObjectId, baseDamage, damageType);
        }

        private Vector3 GetMouseWorldPosition()
        {
            if (UnityEngine.InputSystem.Mouse.current == null) return Vector3.zero;

            Ray ray = _mainCamera.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, GameConstants.GetInteractionRange()))
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
                    enemy.TakeDamage(GameConstants.GetMeleeDamage());
                    PlayMeleeHitClientRpc(enemy.transform.position);
                }
            }
            else
            {
                Debug.LogWarning($"[{nameof(PlayerInteraction)}] Server: EnemyAI NetworkObjectId {enemyNetworkObjectId} not found.");
            }
        }

        [ClientRpc]
        private void PlayMeleeHitClientRpc(Vector3 pos)
        {
            Debug.Log($"[PlayerInteraction] PlayMeleeHitClientRpc at {pos}");
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(SFXType.MeleeHit, pos);
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
                    InventoryComponent gatherer = GetComponent<InventoryComponent>();
                    resourceNode.TakeDamage(GameConstants.GetGatherDamage(), gatherer);
                }
            }
            else
            {
                Debug.LogWarning($"[{nameof(PlayerInteraction)}] Server could not find ResourceNode with NetworkObjectId: {resourceNetworkObjectId}.");
            }
        }

        [ServerRpc]
        private void RequestDestructibleDamageServerRpc(ulong destructibleNetworkObjectId, float baseDamage, DamageType damageType, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (senderClientId != OwnerClientId)
            {
                Debug.LogWarning($"[{nameof(PlayerInteraction)}] Rejecting destructible request: sender={senderClientId} owner={OwnerClientId}.");
                return;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(destructibleNetworkObjectId, out NetworkObject networkObject))
            {
                Debug.LogWarning($"[{nameof(PlayerInteraction)}] Server could not find DestructibleComponent netObj={destructibleNetworkObjectId}.");
                return;
            }

            DestructibleComponent destructible = networkObject.GetComponent<DestructibleComponent>();
            if (destructible == null)
            {
                Debug.LogWarning($"[{nameof(PlayerInteraction)}] Rejecting destructible request: target netObj={destructibleNetworkObjectId} has no {nameof(DestructibleComponent)}.");
                return;
            }

            if (!ValidateDestructibleRequest(senderClientId, destructibleNetworkObjectId, destructible.transform.position))
                return;

            destructible.ApplyDamage(baseDamage, damageType, senderClientId);
        }

        private bool ValidateDestructibleRequest(ulong senderClientId, ulong targetNetworkObjectId, Vector3 targetPosition)
        {
            float maxAllowedDistance = GameConstants.GetInteractionRange() + Mathf.Max(0f, _destructibleServerValidationRangePadding);
            float distance = Vector3.Distance(transform.position, targetPosition);
            if (distance > maxAllowedDistance)
            {
                Debug.LogWarning($"[{nameof(PlayerInteraction)}] Rejecting destructible request from client {senderClientId}: out of range ({distance:0.##} > {maxAllowedDistance:0.##}).");
                return false;
            }

            if (_destructibleRequestCooldownSeconds > 0f)
            {
                float now = Time.unscaledTime;
                if (_lastDestructibleRequestTimes.TryGetValue(targetNetworkObjectId, out float lastRequestTime) &&
                    now - lastRequestTime < _destructibleRequestCooldownSeconds)
                {
                    Debug.LogWarning($"[{nameof(PlayerInteraction)}] Rejecting destructible request from client {senderClientId}: rate-limited for target netObj={targetNetworkObjectId}.");
                    return false;
                }

                _lastDestructibleRequestTimes[targetNetworkObjectId] = now;
            }

            return true;
        }

        private float ResolveCurrentToolDamage()
        {
            float baseDamage = GameConstants.GetGatherDamage();
            if (_equipment == null || _equipment.EquippedWeapon == null)
                return Mathf.Max(0f, baseDamage);

            return Mathf.Max(0f, baseDamage + Mathf.Max(0f, _equipment.EquippedWeapon.DamageBonus));
        }

        private DamageType ResolveCurrentDamageType()
        {
            if (_equipment == null || _equipment.EquippedWeapon == null)
                return DamageType.Blunt;

            string itemName = _equipment.EquippedWeapon.ItemName;
            if (string.IsNullOrWhiteSpace(itemName))
                return DamageType.Blunt;

            string lowerItemName = itemName.ToLowerInvariant();
            if (lowerItemName.Contains("axe"))
                return DamageType.Axe;
            if (lowerItemName.Contains("pick"))
                return DamageType.Pick;
            if (lowerItemName.Contains("fire"))
                return DamageType.Fire;

            return DamageType.Blunt;
        }
    }
}
