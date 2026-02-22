using UnityEngine;
using Unity.Netcode;
using ByteWar.Abilities;

namespace ByteWar.Survival
{
    [RequireComponent(typeof(AttributeSet))]
    public class EquipmentComponent : NetworkBehaviour
    {
        [SerializeField] private Item _equippedWeapon;
        [SerializeField] private Item _equippedArmor;

        /// <summary>Currently equipped weapon (read-only for external consumers).</summary>
        public Item EquippedWeapon => _equippedWeapon;
        /// <summary>Currently equipped armor (read-only for external consumers).</summary>
        public Item EquippedArmor => _equippedArmor;

        private AttributeSet _attributes;

        private void Awake()
        {
            _attributes = GetComponent<AttributeSet>();
        }

        public void EquipItem(Item item)
        {
            if (!IsServer) return;
            if (!item.IsEquippable) return;

            // For PoC, we just assume it's armor if it has armor, else weapon
            if (item.Armor > 0)
            {
                if (_equippedArmor != null) UnequipItem(_equippedArmor);
                _equippedArmor = item;
            }
            else
            {
                if (_equippedWeapon != null) UnequipItem(_equippedWeapon);
                _equippedWeapon = item;
            }

            ApplyItemStats(item, 1);
            Debug.Log($"Equipped {item.ItemName}");
        }

        public void UnequipItem(Item item)
        {
            if (!IsServer) return;

            if (_equippedWeapon == item) _equippedWeapon = null;
            else if (_equippedArmor == item) _equippedArmor = null;
            else return;

            ApplyItemStats(item, -1);
            Debug.Log($"Unequipped {item.ItemName}");
        }

        private void ApplyItemStats(Item item, int multiplier)
        {
            _attributes.MaxHealth.Value += item.HealthBonus * multiplier;
            _attributes.Health.Value += item.HealthBonus * multiplier;

            _attributes.MaxMana.Value += item.ManaBonus * multiplier;
            _attributes.Mana.Value += item.ManaBonus * multiplier;

            // Armor and DamageBonus would be applied to a combat calculation system
        }
    }
}
