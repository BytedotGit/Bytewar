using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using ByteWar.Core;

namespace ByteWar.Abilities
{
    [RequireComponent(typeof(AttributeSet))]
    public class AbilitySystemComponent : NetworkBehaviour, IAbilityExecutor
    {
        public AttributeSet Attributes { get; private set; }

        [SerializeField] private List<Ability> _learnedAbilities = new List<Ability>();
        [SerializeField] private List<Talent> _learnedTalents = new List<Talent>();

        /// <summary>Read-only view of learned abilities.</summary>
        public IReadOnlyList<Ability> LearnedAbilities => _learnedAbilities;
        /// <summary>Read-only view of learned talents.</summary>
        public IReadOnlyList<Talent> LearnedTalents => _learnedTalents;

        private Dictionary<string, float> _cooldownReductions = new Dictionary<string, float>();
        private Dictionary<string, float> _manaCostReductions = new Dictionary<string, float>();

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
            if (_manaCostReductions.TryGetValue(ability.AbilityName, out float manaReduction))
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

            Ability ability = _learnedAbilities[abilityIndex];

            float actualManaCost = ability.ManaCost;
            if (_manaCostReductions.TryGetValue(ability.AbilityName, out float manaReduction))
            {
                actualManaCost = Mathf.Max(0, actualManaCost - manaReduction);
            }

            if (Attributes != null && Attributes.ConsumeMana(actualManaCost))
            {
                ability.Execute(this, targetPosition);

                float actualCooldown = ability.Cooldown;
                if (_cooldownReductions.TryGetValue(ability.AbilityName, out float cdReduction))
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
            Ability ability = _learnedAbilities[abilityIndex];

            // Set cooldown on client for UI tracking
            float actualCooldown = ability.Cooldown;
            if (_cooldownReductions.TryGetValue(ability.AbilityName, out float cdReduction))
            {
                actualCooldown = Mathf.Max(0, actualCooldown - cdReduction);
            }
            _cooldowns[ability.AbilityName] = actualCooldown;

            // Play visual + audio effects on all clients
            Debug.Log($"[AbilitySystemComponent] Playing VFX/SFX for {ability.AbilityName} at {targetPosition}");

            if (Core.VFXManager.Instance != null)
                Core.VFXManager.Instance.PlayEffectLocal(Core.VFXType.FireballMuzzle, targetPosition);

            if (Core.AudioManager.Instance != null)
                Core.AudioManager.Instance.PlaySFX(Core.SFXType.FireballCast, targetPosition);
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

        // ── Mutation methods ──────────────────────────────────────────────────

        /// <summary>Add an ability to the learned list. Use for setup/tests.</summary>
        public void AddLearnedAbility(Ability ability)
        {
            _learnedAbilities.Add(ability);
        }

        /// <summary>Set a cooldown reduction for the named ability.</summary>
        public void SetCooldownReduction(string abilityName, float reduction)
        {
            _cooldownReductions[abilityName] = reduction;
        }

        /// <summary>Get cooldown reduction for the named ability.</summary>
        public float GetCooldownReduction(string abilityName)
        {
            return _cooldownReductions.TryGetValue(abilityName, out float val) ? val : 0f;
        }

        /// <summary>Set a mana cost reduction for the named ability.</summary>
        public void SetManaCostReduction(string abilityName, float reduction)
        {
            _manaCostReductions[abilityName] = reduction;
        }

        /// <summary>Get mana cost reduction for the named ability.</summary>
        public float GetManaCostReduction(string abilityName)
        {
            return _manaCostReductions.TryGetValue(abilityName, out float val) ? val : 0f;
        }

        public void LearnTalent(Talent talent)
        {
            if (!IsServer) return;
            if (!_learnedTalents.Contains(talent))
            {
                _learnedTalents.Add(talent);
                talent.ApplyTalent(this);
            }
        }
    }
}
