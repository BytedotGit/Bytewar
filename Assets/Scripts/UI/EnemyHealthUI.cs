using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using SurvivalRPG.Survival;
using SurvivalRPG.Abilities;

namespace SurvivalRPG.UI
{
    /// <summary>
    /// Displays the health of the currently targeted enemy in the HUD.
    /// Binds to the local player's last clicked EnemyAI via the event
    /// <see cref="EnemyTargetTracker.OnEnemyTargeted"/>.
    /// </summary>
    public class EnemyHealthUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _healthText;

        private AttributeSet _targetAttributes;
        private string _targetName;

        private void OnEnable()
        {
            EnemyTargetTracker.OnEnemyTargeted += OnEnemyTargeted;
        }

        private void OnDisable()
        {
            EnemyTargetTracker.OnEnemyTargeted -= OnEnemyTargeted;
            UnsubscribeFromTarget();
        }

        private void OnEnemyTargeted(EnemyAI enemy)
        {
            UnsubscribeFromTarget();

            if (enemy == null)
            {
                HidePanel();
                return;
            }

            _targetAttributes = enemy.GetComponent<AttributeSet>();
            _targetName = enemy.gameObject.name;

            if (_targetAttributes != null)
            {
                _targetAttributes.Health.OnValueChanged += OnTargetHealthChanged;
                Debug.Log($"[EnemyHealthUI] Targeting '{_targetName}'. Health={_targetAttributes.Health.Value}");
                ShowPanel();
                RefreshUI();
            }
            else
            {
                Debug.LogWarning($"[EnemyHealthUI] Targeted enemy '{_targetName}' has no AttributeSet.");
                HidePanel();
            }
        }

        private void OnTargetHealthChanged(float previous, float current)
        {
            Debug.Log($"[EnemyHealthUI] '{_targetName}' health changed: {previous:F1} → {current:F1}");
            if (current <= 0f)
            {
                HidePanel();
                UnsubscribeFromTarget();
            }
            else
            {
                RefreshUI();
            }
        }

        private void RefreshUI()
        {
            if (_targetAttributes == null) return;

            if (_nameText != null) _nameText.text = _targetName;

            float maxHealth = _targetAttributes.MaxHealth.Value;
            float currentHealth = _targetAttributes.Health.Value;

            if (_healthSlider != null)
            {
                _healthSlider.maxValue = maxHealth;
                _healthSlider.value = currentHealth;
            }

            if (_healthText != null)
                _healthText.text = $"{Mathf.RoundToInt(currentHealth)} / {Mathf.RoundToInt(maxHealth)}";
        }

        private void ShowPanel()
        {
            if (_panel != null) _panel.SetActive(true);
        }

        private void HidePanel()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void UnsubscribeFromTarget()
        {
            if (_targetAttributes != null)
            {
                _targetAttributes.Health.OnValueChanged -= OnTargetHealthChanged;
                _targetAttributes = null;
            }
        }
    }
}
