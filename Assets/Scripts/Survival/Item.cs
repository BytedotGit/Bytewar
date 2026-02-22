using UnityEngine;

namespace ByteWar.Survival
{
    [CreateAssetMenu(fileName = "NewItem", menuName = "ByteWar/Survival/Item")]
    public class Item : ScriptableObject
    {
        public string ItemName;
        public string Description;
        public Sprite Icon;
        public bool IsEquippable;

        [Header("Equipment Stats")]
        public float HealthBonus;
        public float ManaBonus;
        public float Armor;
        public float DamageBonus;
    }
}
