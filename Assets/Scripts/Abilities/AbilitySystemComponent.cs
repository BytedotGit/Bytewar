using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace ByteWar.Abilities
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
            EnsureAttributesAssigned("Awake");
        }

        private void OnEnable()
        {
            // Some NGO test harnesses instantiate prefabs inactive and then activate clones.
            // Ensure Attributes is always available even if Awake ordering gets unusual.
            EnsureAttributesAssigned("OnEnable");
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            EnsureAttributesAssigned("OnNetworkSpawn");
        }

        private void EnsureAttributesAssigned(string context)
        {
            if (Attributes != null) return;

            Attributes = GetComponent<AttributeSet>();
            if (Attributes == null)
            {
                Debug.LogError($"[AbilitySystemComponent] Attributes missing (context={context}) on '{gameObject.name}'. This should never happen due to [RequireComponent].");
                return;
            }

            // Only log when we had to repair a null reference to avoid log noise.
            if (context != "Awake")
            {
                Debug.LogWarning($"[AbilitySystemComponent] Repaired null Attributes via GetComponent (context={context}) on '{gameObject.name}'.");
            }
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

        public bool TryCastAbility(int abilityIndex, Vector3 targetPosition)
        {
            EnsureAttributesAssigned("TryCastAbility");

            if (abilityIndex < 0 || abilityIndex >= LearnedAbilities.Count)
            {
                Debug.LogWarning($"[AbilitySystemComponent] TryCastAbility failed: index {abilityIndex} out of range (count={LearnedAbilities.Count}).");
                return false;
            }

            Ability ability = LearnedAbilities[abilityIndex];
            if (ability == null)
            {
                Debug.LogWarning($"[AbilitySystemComponent] TryCastAbility failed: ability at index {abilityIndex} is null.");
                return false;
            }

            if (IsOnCooldown(ability.AbilityName))
            {
                Debug.Log($"{ability.AbilityName} is on cooldown.");
                return false;
            }

            // Local preflight: avoid animation/no-op when mana is obviously insufficient.
            float actualManaCost = ability.ManaCost;
            if (ManaCostReductions.TryGetValue(ability.AbilityName, out float manaReduction))
            {
                actualManaCost = Mathf.Max(0f, actualManaCost - manaReduction);
            }

            if (Attributes == null)
            {
                Debug.LogWarning("[AbilitySystemComponent] TryCastAbility failed: Attributes is null.");
                return false;
            }

            if (Attributes.Mana.Value < actualManaCost)
            {
                Debug.Log($"[AbilitySystemComponent] Not enough mana to cast {ability.AbilityName}. Need={actualManaCost:0.0} Have={Attributes.Mana.Value:0.0}");
                return false;
            }

            CastAbilityServerRpc(abilityIndex, targetPosition);
            return true;
        }

        [ServerRpc]
        private void CastAbilityServerRpc(int abilityIndex, Vector3 targetPosition)
        {
            EnsureAttributesAssigned("CastAbilityServerRpc");

            Ability ability = LearnedAbilities[abilityIndex];

            float actualManaCost = ability.ManaCost;
            if (ManaCostReductions.TryGetValue(ability.AbilityName, out float manaReduction))
            {
                actualManaCost = Mathf.Max(0, actualManaCost - manaReduction);
            }

            if (Attributes != null && Attributes.ConsumeMana(actualManaCost))
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
