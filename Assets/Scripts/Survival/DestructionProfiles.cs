using System.Collections.Generic;
using UnityEngine;

namespace ByteWar.Survival
{
    public enum DamageType
    {
        Axe,
        Pick,
        Fire,
        Blunt,
    }

    public enum DestructibleClass
    {
        Tree,
        Rock,
        BuildPiece,
        OreNode,
    }

    public enum DestructibleStateType
    {
        Intact,
        Damaged,
        Destroyed,
    }

    [System.Serializable]
    public struct DamageTypeMultiplier
    {
        [SerializeField] private DamageType _damageType;
        [SerializeField] private float _multiplier;

        public DamageType DamageType { get => _damageType; internal set => _damageType = value; }
        public float Multiplier { get => _multiplier; internal set => _multiplier = value; }
    }

    [System.Serializable]
    public struct DestructibleStateThreshold
    {
        [SerializeField] private DestructibleStateType _state;
        [SerializeField] private float _normalizedHealthThreshold;

        public DestructibleStateType State { get => _state; internal set => _state = value; }
        public float NormalizedHealthThreshold { get => _normalizedHealthThreshold; internal set => _normalizedHealthThreshold = value; }
    }

    [CreateAssetMenu(fileName = "NewDestructibleProfile", menuName = "ByteWar/Survival/Destruction/DestructibleProfile")]
    public class DestructibleProfile : ScriptableObject
    {
        [SerializeField] private string _profileId = string.Empty;
        [SerializeField] private DestructibleClass _destructibleClass = DestructibleClass.Tree;
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private List<DamageTypeMultiplier> _damageMultipliers = new();
        [SerializeField] private List<DestructibleStateThreshold> _stateThresholds = new();
        [SerializeField] private string _dropTableId = string.Empty;

        public string ProfileId { get => _profileId; internal set => _profileId = value; }
        public DestructibleClass DestructibleClass { get => _destructibleClass; internal set => _destructibleClass = value; }
        public float MaxHealth { get => _maxHealth; internal set => _maxHealth = value; }
        public IReadOnlyList<DamageTypeMultiplier> DamageMultipliers => _damageMultipliers;
        public IReadOnlyList<DestructibleStateThreshold> StateThresholds => _stateThresholds;
        public string DropTableId { get => _dropTableId; internal set => _dropTableId = value; }

        internal List<DamageTypeMultiplier> DamageMultipliersMutable => _damageMultipliers;
        internal List<DestructibleStateThreshold> StateThresholdsMutable => _stateThresholds;

        public float GetDamageMultiplier(DamageType damageType)
        {
            for (int i = 0; i < _damageMultipliers.Count; i++)
            {
                if (_damageMultipliers[i].DamageType == damageType)
                {
                    return Mathf.Max(0f, _damageMultipliers[i].Multiplier);
                }
            }

            return 1f;
        }
    }

    [CreateAssetMenu(fileName = "NewSupportProfile", menuName = "ByteWar/Survival/Destruction/SupportProfile")]
    public class SupportProfile : ScriptableObject
    {
        [SerializeField] private string _profileId = string.Empty;
        [SerializeField] private float _maxSupportDistance = 8f;
        [SerializeField] private float _collapseDelaySeconds = 0.25f;
        [SerializeField] private bool _allowDiagonalSupport = true;

        public string ProfileId { get => _profileId; internal set => _profileId = value; }
        public float MaxSupportDistance { get => _maxSupportDistance; internal set => _maxSupportDistance = value; }
        public float CollapseDelaySeconds { get => _collapseDelaySeconds; internal set => _collapseDelaySeconds = value; }
        public bool AllowDiagonalSupport { get => _allowDiagonalSupport; internal set => _allowDiagonalSupport = value; }
    }
}