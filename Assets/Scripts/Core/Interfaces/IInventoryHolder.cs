using System.Collections.Generic;
using ByteWar.Survival;

namespace ByteWar.Core
{
    /// <summary>
    /// Any entity that holds an inventory. Implemented by InventoryComponent.
    /// </summary>
    public interface IInventoryHolder
    {
        /// <summary>Read-only view of held items.</summary>
        IReadOnlyList<Item> Items { get; }

        /// <summary>Add an item to the inventory. Server-authoritative.</summary>
        void AddItem(Item item);

        /// <summary>Check whether the inventory contains at least <paramref name="count"/> of <paramref name="item"/>.</summary>
        bool HasItem(Item item, int count);

        /// <summary>Remove up to <paramref name="count"/> of <paramref name="item"/>. Server-authoritative.</summary>
        void RemoveItem(Item item, int count);
    }
}
