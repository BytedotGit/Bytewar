using System;
using UnityEngine;

namespace ByteWar.Survival
{
    /// <summary>
    /// Network-safe equipped item state storing only deterministic item IDs.
    /// </summary>
    [Serializable]
    public struct EquippedLoadoutState
    {
        [SerializeField] private string _helmetItemId;
        [SerializeField] private string _chestItemId;
        [SerializeField] private string _legsItemId;
        [SerializeField] private string _glovesItemId;
        [SerializeField] private string _bootsItemId;
        [SerializeField] private string _mainHandItemId;
        [SerializeField] private string _offHandItemId;
        [SerializeField] private string _backItemId;

        public string GetItemId(GearSlot slot)
        {
            return (slot switch
            {
                GearSlot.Helmet => _helmetItemId,
                GearSlot.Chest => _chestItemId,
                GearSlot.Legs => _legsItemId,
                GearSlot.Gloves => _glovesItemId,
                GearSlot.Boots => _bootsItemId,
                GearSlot.MainHand => _mainHandItemId,
                GearSlot.OffHand => _offHandItemId,
                GearSlot.Back => _backItemId,
                _ => string.Empty,
            }) ?? string.Empty;
        }

        public void SetItemId(GearSlot slot, string itemId)
        {
            string value = itemId ?? string.Empty;

            switch (slot)
            {
                case GearSlot.Helmet:
                    _helmetItemId = value;
                    break;
                case GearSlot.Chest:
                    _chestItemId = value;
                    break;
                case GearSlot.Legs:
                    _legsItemId = value;
                    break;
                case GearSlot.Gloves:
                    _glovesItemId = value;
                    break;
                case GearSlot.Boots:
                    _bootsItemId = value;
                    break;
                case GearSlot.MainHand:
                    _mainHandItemId = value;
                    break;
                case GearSlot.OffHand:
                    _offHandItemId = value;
                    break;
                case GearSlot.Back:
                    _backItemId = value;
                    break;
            }
        }

        public EquippedLoadoutState WithItemId(GearSlot slot, string itemId)
        {
            var copy = this;
            copy.SetItemId(slot, itemId);
            return copy;
        }
    }
}