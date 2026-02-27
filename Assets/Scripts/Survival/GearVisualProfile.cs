using UnityEngine;

namespace ByteWar.Survival
{
    [System.Flags]
    public enum BodyMaskFlags
    {
        None = 0,
        Head = 1 << 0,
        Torso = 1 << 1,
        Arms = 1 << 2,
        Hands = 1 << 3,
        Legs = 1 << 4,
        Feet = 1 << 5,
    }

    public enum GearSlot
    {
        Helmet,
        Chest,
        Legs,
        Gloves,
        Boots,
        MainHand,
        OffHand,
        Back,
    }

    public enum GearVisualType
    {
        SkinnedReplacement,
        SocketAttachment,
    }

    [CreateAssetMenu(fileName = "NewGearVisualProfile", menuName = "ByteWar/Survival/GearVisualProfile")]
    public class GearVisualProfile : ScriptableObject
    {
        [SerializeField] private string _itemId = string.Empty;
        [SerializeField] private GearSlot _slot = GearSlot.Helmet;
        [SerializeField] private GearVisualType _visualType = GearVisualType.SkinnedReplacement;
        [SerializeField] private GameObject _visualPrefab;
        [SerializeField] private Mesh _skinnedMeshOverride;
        [SerializeField] private string _socketName = string.Empty;
        [SerializeField] private BodyMaskFlags _bodyMaskFlags = BodyMaskFlags.None;

        public string ItemId { get => _itemId; internal set => _itemId = value; }
        public GearSlot Slot { get => _slot; internal set => _slot = value; }
        public GearVisualType VisualType { get => _visualType; internal set => _visualType = value; }
        public GameObject VisualPrefab { get => _visualPrefab; internal set => _visualPrefab = value; }
        public Mesh SkinnedMeshOverride { get => _skinnedMeshOverride; internal set => _skinnedMeshOverride = value; }
        public string SocketName { get => _socketName; internal set => _socketName = value; }
        public BodyMaskFlags BodyMask { get => _bodyMaskFlags; internal set => _bodyMaskFlags = value; }
    }
}