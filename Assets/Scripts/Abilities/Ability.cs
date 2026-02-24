using UnityEngine;
using ByteWar.Core;

namespace ByteWar.Abilities
{
    public abstract class Ability : ScriptableObject
    {
        [SerializeField] private string _abilityName;
        [SerializeField] private float _manaCost;
        [SerializeField] private float _cooldown;
        [SerializeField] private VFXType _castVFXType = VFXType.FireballMuzzle;
        [SerializeField] private SFXType _castSFXType = SFXType.FireballCast;

        public string AbilityName { get => _abilityName; internal set => _abilityName = value; }
        public float ManaCost { get => _manaCost; internal set => _manaCost = value; }
        public float Cooldown { get => _cooldown; internal set => _cooldown = value; }
        public VFXType CastVFXType { get => _castVFXType; internal set => _castVFXType = value; }
        public SFXType CastSFXType { get => _castSFXType; internal set => _castSFXType = value; }

        public abstract void Execute(AbilitySystemComponent caster, Vector3 targetPosition);
    }
}
