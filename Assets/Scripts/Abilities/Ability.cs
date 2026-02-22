using UnityEngine;

namespace ByteWar.Abilities
{
    public abstract class Ability : ScriptableObject
    {
        [SerializeField] private string _abilityName;
        [SerializeField] private float _manaCost;
        [SerializeField] private float _cooldown;

        public string AbilityName { get => _abilityName; internal set => _abilityName = value; }
        public float ManaCost { get => _manaCost; internal set => _manaCost = value; }
        public float Cooldown { get => _cooldown; internal set => _cooldown = value; }

        public abstract void Execute(AbilitySystemComponent caster, Vector3 targetPosition);
    }
}
