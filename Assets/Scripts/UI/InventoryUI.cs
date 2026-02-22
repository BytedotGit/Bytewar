using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using ByteWar.Survival;
using System.Collections.Generic;

namespace ByteWar.UI
{
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private Transform itemContainer;
        [SerializeField] private GameObject itemPrefab; // Prefab with TextMeshProUGUI

        private InventoryComponent _inventory;
        private List<GameObject> _spawnedItems = new List<GameObject>();
        private bool _dirty = false;

        private void Update()
        {
            if (_inventory == null)
            {
                FindLocalPlayerInventory();
                return;
            }

            // Rebuild only when flagged dirty (via event) rather than every frame
            if (_dirty)
            {
                _dirty = false;
                RebuildUI();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeInventoryEvents();
        }

        private void FindLocalPlayerInventory()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            {
                var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
                if (localPlayer != null)
                {
                    _inventory = localPlayer.GetComponent<InventoryComponent>();
                    if (_inventory != null)
                    {
                        Debug.Log("[InventoryUI] Bound to local player InventoryComponent. Subscribing to events.");
                        _inventory.OnItemAdded += OnInventoryChanged;
                        _inventory.OnItemRemoved += OnInventoryChanged;
                        _dirty = true; // Initial populate
                    }
                }
            }
        }

        private void UnsubscribeInventoryEvents()
        {
            if (_inventory != null)
            {
                _inventory.OnItemAdded -= OnInventoryChanged;
                _inventory.OnItemRemoved -= OnInventoryChanged;
            }
        }

        private void OnInventoryChanged(string itemName)
        {
            Debug.Log($"[InventoryUI] Inventory changed (item: '{itemName}'). Flagging dirty.");
            _dirty = true;
        }

        private void RebuildUI()
        {
            if (_inventory == null || itemContainer == null || itemPrefab == null) return;

            // Group items by name
            Dictionary<string, int> itemCounts = new Dictionary<string, int>();
            foreach (var item in _inventory.Items)
            {
                if (item != null)
                {
                    if (itemCounts.ContainsKey(item.ItemName))
                        itemCounts[item.ItemName]++;
                    else
                        itemCounts[item.ItemName] = 1;
                }
            }

            // Clear old UI
            foreach (var obj in _spawnedItems)
            {
                Destroy(obj);
            }
            _spawnedItems.Clear();

            // Spawn new UI
            foreach (var kvp in itemCounts)
            {
                GameObject go = Instantiate(itemPrefab, itemContainer);
                TextMeshProUGUI text = go.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.text = $"{kvp.Key} x{kvp.Value}";
                _spawnedItems.Add(go);
            }

            Debug.Log($"[InventoryUI] Rebuilt with {itemCounts.Count} unique item types.");
        }
    }
}