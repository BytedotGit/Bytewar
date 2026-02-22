using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;

namespace ByteWar.Survival
{
    /// <summary>
    /// Manages a player's inventory.  Items are stored server-side; add/remove
    /// operations fire ClientRpc notifications so all clients (e.g. the owner's UI)
    /// can react without requiring a full NetworkList synchronisation.
    /// </summary>
    public class InventoryComponent : NetworkBehaviour
    {
        // Server-authoritative item list (ScriptableObject references)
        public List<Item> Items = new List<Item>();

        /// <summary>Raised on every client when an item is added. Parameter: item name.</summary>
        public event Action<string> OnItemAdded;

        /// <summary>Raised on every client when an item is removed. Parameter: item name.</summary>
        public event Action<string> OnItemRemoved;

        // ── Public API ────────────────────────────────────────────────────────────

        public void AddItem(Item item)
        {
            // Server-authoritative when NGO is active. In offline contexts (EditMode tests or
            // local non-networked usage), allow logic to run without a NetworkManager.
            if (!IsServer && NetworkManager.Singleton != null) return;

            Items.Add(item);
            Debug.Log($"[InventoryComponent] Added '{item.ItemName}' to {gameObject.name}'s inventory. Total: {Items.Count}");

            // Only dispatch RPCs when NGO is active and this object is spawned.
            if (NetworkManager.Singleton != null && IsSpawned)
            {
                NotifyItemAddedClientRpc(item.ItemName);
            }
            else
            {
                OnItemAdded?.Invoke(item.ItemName);
            }
        }

        public bool HasItem(Item item, int count)
        {
            int found = 0;
            foreach (var i in Items)
            {
                if (i == item) found++;
            }
            return found >= count;
        }

        public void RemoveItem(Item item, int count)
        {
            if (!IsServer && NetworkManager.Singleton != null) return;

            int removed = 0;
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (Items[i] == item)
                {
                    Items.RemoveAt(i);
                    removed++;
                    if (removed >= count) break;
                }
            }

            Debug.Log($"[InventoryComponent] Removed {removed}x '{item.ItemName}' from {gameObject.name}'s inventory.");
            if (removed > 0)
            {
                if (NetworkManager.Singleton != null && IsSpawned)
                {
                    NotifyItemRemovedClientRpc(item.ItemName);
                }
                else
                {
                    OnItemRemoved?.Invoke(item.ItemName);
                }
            }
        }

        // ── ClientRpc notifications ───────────────────────────────────────────────

        [ClientRpc]
        private void NotifyItemAddedClientRpc(string itemName)
        {
            Debug.Log($"[InventoryComponent] (Client) Item added to inventory: '{itemName}'.");
            OnItemAdded?.Invoke(itemName);
        }

        [ClientRpc]
        private void NotifyItemRemovedClientRpc(string itemName)
        {
            Debug.Log($"[InventoryComponent] (Client) Item removed from inventory: '{itemName}'.");
            OnItemRemoved?.Invoke(itemName);
        }
    }
}
