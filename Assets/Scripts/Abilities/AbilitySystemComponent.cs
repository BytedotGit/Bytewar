using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace SurvivalRPG.Abilities
{
    [RequireComponent(typeof(AttributeSet))]
    public class AbilitySystemComponent : NetworkBehaviour
    {
        public AttributeSet Attributes { get; private set; }
        public List<Ability> LearnedAbilities = new List<Ability>();
        public List<Talent> LearnedTalents = new List<Talent>();

        public Dictionary<string, float> CooldownReductions = new Dictionary<string, float>();
        public Dictionary<string, float> ManaCostReductions = new Dictionary<string, float>();

        private Dictionary<string, float> _cooldowns = new Dictionary<string, float>();

        private void Awake()
        {
            Attributes = GetComponent<AttributeSet>();
        }

        // Reusable list to avoid GC allocation every frame
        private readonly List<string> _cdKeysBuffer = new List<string>();

        private void Update()
        {
            if (_cooldowns.Count == 0) return;
            _cdKeysBuffer.Clear();
            _cdKeysBuffer.AddRange(_cooldowns.Keys);
            foreach (var key in _cdKeysBuffer)
            {
                if (_cooldowns[key] > 0)
                    _cooldowns[key] -= Time.deltaTime;
            }
        }

        public void TryCastAbility(int abilityIndex, Vector3 targetPosition)
        {
            if (abilityIndex < 0 || abilityIndex >= LearnedAbilities.Count) return;

            Ability ability = LearnedAbilities[abilityIndex];
            if (IsOnCooldown(ability.AbilityName))
            {
                Debug.Log($"{ability.AbilityName} is on cooldown.");
                return;
            }

            CastAbilityServerRpc(abilityIndex, targetPosition);
        }

        [ServerRpc]
        private void CastAbilityServerRpc(int abilityIndex, Vector3 targetPosition)
        {
            Ability ability = LearnedAbilities[abilityIndex];

            float actualManaCost = ability.ManaCost;
            if (ManaCostReductions.TryGetValue(ability.AbilityName, out float manaReduction))
            {
                actualManaCost = Mathf.Max(0, actualManaCost - manaReduction);
            }

            if (Attributes.ConsumeMana(actualManaCost))
            {
                ability.Execute(this, targetPosition);

                float actualCooldown = ability.Cooldown;
                if (CooldownReductions.TryGetValue(ability.AbilityName, out float cdReduction))
                {
                    actualCooldown = Mathf.Max(0, actualCooldown - cdReduction);
                }

                _cooldowns[ability.AbilityName] = actualCooldown;
                PlayAbilityEffectClientRpc(abilityIndex, targetPosition);
            }
            else
            {
                Debug.Log("Not enough mana.");
            }
        }

        [ClientRpc]
        private void PlayAbilityEffectClientRpc(int abilityIndex, Vector3 targetPosition)
        {
            Ability ability = LearnedAbilities[abilityIndex];

            // Set cooldown on client for UI tracking
            float actualCooldown = ability.Cooldown;
            if (CooldownReductions.TryGetValue(ability.AbilityName, out float cdReduction))
            {
                actualCooldown = Mathf.Max(0, actualCooldown - cdReduction);
            }
            _cooldowns[ability.AbilityName] = actualCooldown;

            // Play visual effects on clients
            Debug.Log($"Playing VFX for {ability.AbilityName} at {targetPosition}");
        }

        public float GetRemainingCooldown(string abilityName)
        {
            if (_cooldowns.TryGetValue(abilityName, out float cd))
            {
                return Mathf.Max(0, cd);
            }
            return 0f;
        }

        public bool IsOnCooldown(string abilityName)
        {
            return _cooldowns.ContainsKey(abilityName) && _cooldowns[abilityName] > 0;
        }

        public void LearnTalent(Talent talent)
        {
            if (!IsServer) return;
            if (!LearnedTalents.Contains(talent))
            {
                LearnedTalents.Add(talent);
                talent.ApplyTalent(this);
            }
        }
    }
}
