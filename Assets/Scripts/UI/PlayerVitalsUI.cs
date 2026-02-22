using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using ByteWar.Abilities;

namespace ByteWar.UI
{
    public class PlayerVitalsUI : MonoBehaviour
    {
        public Slider healthSlider;
        [SerializeField] private TextMeshProUGUI healthText;
        public Slider manaSlider;
        [SerializeField] private TextMeshProUGUI manaText;
        public Slider staminaSlider;
        [SerializeField] private TextMeshProUGUI staminaText;

        private AttributeSet _playerAttributes;

        private void Update()
        {
            if (_playerAttributes == null)
            {
                FindLocalPlayerAttributes();
                return;
            }

            UpdateUI();
        }

        private void FindLocalPlayerAttributes()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            {
                var localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
                if (localPlayer != null)
                {
                    _playerAttributes = localPlayer.GetComponent<AttributeSet>();
                    if (_playerAttributes != null)
                    {
                        Debug.Log("PlayerVitalsUI bound to local player AttributeSet.");
                        _playerAttributes.Health.OnValueChanged += OnHealthChanged;
                        _playerAttributes.Mana.OnValueChanged += OnManaChanged;
                        _playerAttributes.Stamina.OnValueChanged += OnStaminaChanged;

                        UpdateUI();
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (_playerAttributes != null)
            {
                _playerAttributes.Health.OnValueChanged -= OnHealthChanged;
                _playerAttributes.Mana.OnValueChanged -= OnManaChanged;
                _playerAttributes.Stamina.OnValueChanged -= OnStaminaChanged;
            }
        }

        private void OnHealthChanged(float previousValue, float newValue) => UpdateUI();
        private void OnManaChanged(float previousValue, float newValue) => UpdateUI();
        private void OnStaminaChanged(float previousValue, float newValue) => UpdateUI();

        private void UpdateUI()
        {
            if (_playerAttributes == null) return;

            if (healthSlider != null)
            {
                healthSlider.maxValue = _playerAttributes.MaxHealth.Value;
                healthSlider.value = _playerAttributes.Health.Value;
            }
            if (healthText != null) healthText.text = $"HP: {Mathf.RoundToInt(_playerAttributes.Health.Value)}/{Mathf.RoundToInt(_playerAttributes.MaxHealth.Value)}";

            if (manaSlider != null)
            {
                manaSlider.maxValue = _playerAttributes.MaxMana.Value;
                manaSlider.value = _playerAttributes.Mana.Value;
            }
            if (manaText != null) manaText.text = $"MP: {Mathf.RoundToInt(_playerAttributes.Mana.Value)}/{Mathf.RoundToInt(_playerAttributes.MaxMana.Value)}";

            if (staminaSlider != null)
            {
                staminaSlider.maxValue = _playerAttributes.MaxStamina.Value;
                staminaSlider.value = _playerAttributes.Stamina.Value;
            }
            if (staminaText != null) staminaText.text = $"SP: {Mathf.RoundToInt(_playerAttributes.Stamina.Value)}/{Mathf.RoundToInt(_playerAttributes.MaxStamina.Value)}";
        }
    }
}