using UnityEngine;
using Unity.Netcode;

namespace ByteWar.Survival
{
    public class ResourceNode : NetworkBehaviour
    {
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private Item _dropItem;
        [SerializeField] private int _dropAmount = 1;

        public NetworkVariable<float> Health = new NetworkVariable<float>(100f);

        public void SetupForTest(Item dropItem, int dropAmount)
        {
            _dropItem = dropItem;
            _dropAmount = dropAmount;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Health.Value = _maxHealth;
                Debug.Log($"[ResourceNode] Spawned {gameObject.name} with {Health.Value} health.");
            }
        }

        public void TakeDamage(float amount, InventoryComponent gatherer = null)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"[ResourceNode] TakeDamage called on client for {gameObject.name}. Ignoring.");
                return;
            }

            Health.Value -= amount;
            Debug.Log($"[ResourceNode] {gameObject.name} took {amount} damage. Remaining health: {Health.Value}");

            if (Health.Value <= 0)
            {
                Die(gatherer);
            }
        }

        private void Die(InventoryComponent gatherer)
        {
            Debug.Log($"[ResourceNode] {gameObject.name} destroyed. Dropping items.");
            DropItems(gatherer);

            // Despawn and destroy the network object
            NetworkObject.Despawn(true);
        }

        private void DropItems(InventoryComponent gatherer)
        {
            if (_dropItem != null)
            {
                Debug.Log($"[ResourceNode] Dropping {_dropAmount}x {_dropItem.ItemName} from {gameObject.name}.");
                if (gatherer != null)
                {
                    for (int i = 0; i < _dropAmount; i++)
                    {
                        gatherer.AddItem(_dropItem);
                    }
                }
                // In a full implementation, we would spawn a dropped item prefab here if gatherer is null
            }
            else
            {
                Debug.LogWarning($"[ResourceNode] No drop item assigned for {gameObject.name}.");
            }
        }
    }
}