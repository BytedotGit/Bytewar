using UnityEngine;

namespace ByteWar.Survival
{
    [CreateAssetMenu(fileName = "NewItem", menuName = "ByteWar/Survival/Item")]
    public class Item : ScriptableObject
    {
        [SerializeField] private string _itemName;
        [SerializeField] private string _description;
        [SerializeField] private Sprite _icon;
        [SerializeField] private bool _isEquippable;

        [Header("Equipment Stats")]
        [SerializeField] private float _healthBonus;
        [SerializeField] private float _manaBonus;
        [SerializeField] private float _armor;
        [SerializeField] private float _damageBonus;

        public string ItemName { get => _itemName; internal set => _itemName = value; }
        public string Description { get => _description; internal set => _description = value; }
        public Sprite Icon => _icon;
        public bool IsEquippable { get => _isEquippable; internal set => _isEquippable = value; }
        public float HealthBonus { get => _healthBonus; internal set => _healthBonus = value; }
        public float ManaBonus { get => _manaBonus; internal set => _manaBonus = value; }
        public float Armor { get => _armor; internal set => _armor = value; }
        public float DamageBonus { get => _damageBonus; internal set => _damageBonus = value; }
    }
}
