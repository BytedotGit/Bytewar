using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using SurvivalRPG.Abilities;
using System.Collections.Generic;

namespace SurvivalRPG.UI
{
    public class ActionBarUI : MonoBehaviour
    {
        [System.Serializable]
        public class ActionSlot
        {
            public Button Button;
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI CooldownText;
            public Image CooldownOverlay;
        }

        [SerializeField] private List<ActionSlot> actionSlots = new List<ActionSlot>();

        private AbilitySystemComponent _abilitySystem;

        private void Update()
        {
            if (_abilitySystem == null)
            {
                FindLocalPlayerAbilitySystem();
                return;
            }

            UpdateUI();
        }

        private void FindLocalPlayerAbilitySystem()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            {
                var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
                if (localPlayer != null)
                {
                    _abilitySystem = localPlayer.GetComponent<AbilitySystemComponent>();
                    if (_abilitySystem != null)
                    {
                        Debug.Log("ActionBarUI bound to local player AbilitySystemComponent.");
                        SetupButtons();
                    }
                }
            }
        }

        private void SetupButtons()
        {
            for (int i = 0; i < actionSlots.Count; i++)
            {
                int index = i; // Capture for closure
                if (actionSlots[i].Button != null)
                {
                    actionSlots[i].Button.onClick.RemoveAllListeners();
                    actionSlots[i].Button.onClick.AddListener(() => OnSlotClicked(index));
                }
            }
        }

        private void OnSlotClicked(int index)
        {
            if (_abilitySystem == null) return;

            // For testing, we just cast at the player's position or forward
            Vector3 targetPos = _abilitySystem.transform.position + _abilitySystem.transform.forward * 5f;
            _abilitySystem.TryCastAbility(index, targetPos);
            Debug.Log($"ActionBarUI: Clicked slot {index}");
        }

        private void UpdateUI()
        {
            if (_abilitySystem == null) return;

            for (int i = 0; i < actionSlots.Count; i++)
            {
                var slot = actionSlots[i];
                if (i < _abilitySystem.LearnedAbilities.Count)
                {
                    Ability ability = _abilitySystem.LearnedAbilities[i];
                    if (ability != null)
                    {
                        if (slot.NameText != null) slot.NameText.text = ability.AbilityName;

                        float remainingCd = _abilitySystem.GetRemainingCooldown(ability.AbilityName);
                        bool isOnCooldown = remainingCd > 0;

                        if (slot.CooldownText != null)
                        {
                            slot.CooldownText.text = isOnCooldown ? remainingCd.ToString("F1") : "";
                        }

                        if (slot.CooldownOverlay != null)
                        {
                            slot.CooldownOverlay.fillAmount = isOnCooldown ? (remainingCd / ability.Cooldown) : 0f;
                        }

                        if (slot.Button != null)
                        {
                            slot.Button.interactable = !isOnCooldown;
                        }
                    }
                }
                else
                {
                    if (slot.NameText != null) slot.NameText.text = "Empty";
                    if (slot.CooldownText != null) slot.CooldownText.text = "";
                    if (slot.CooldownOverlay != null) slot.CooldownOverlay.fillAmount = 0f;
                    if (slot.Button != null) slot.Button.interactable = false;
                }
            }
        }
    }
}