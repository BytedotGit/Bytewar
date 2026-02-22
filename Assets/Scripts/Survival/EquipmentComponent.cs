using UnityEngine;
using Unity.Netcode;
using SurvivalRPG.Abilities;

namespace SurvivalRPG.Survival
{
    [RequireComponent(typeof(AttributeSet))]
    public class EquipmentComponent : NetworkBehaviour
    {
        public Item EquippedWeapon;
        public Item EquippedArmor;

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
                if (EquippedArmor != null) UnequipItem(EquippedArmor);
                EquippedArmor = item;
            }
            else
            {
                if (EquippedWeapon != null) UnequipItem(EquippedWeapon);
                EquippedWeapon = item;
            }

            ApplyItemStats(item, 1);
            Debug.Log($"Equipped {item.ItemName}");
        }

        public void UnequipItem(Item item)
        {
            if (!IsServer) return;

            if (EquippedWeapon == item) EquippedWeapon = null;
            else if (EquippedArmor == item) EquippedArmor = null;
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
