using UnityEngine;
using Unity.Netcode;

namespace SurvivalRPG.Abilities
{
    public class AttributeSet : NetworkBehaviour
    {
        public NetworkVariable<float> Health = new NetworkVariable<float>(100f);
        public NetworkVariable<float> MaxHealth = new NetworkVariable<float>(100f);

        public NetworkVariable<float> Mana = new NetworkVariable<float>(100f);
        public NetworkVariable<float> MaxMana = new NetworkVariable<float>(100f);

        public NetworkVariable<float> Stamina = new NetworkVariable<float>(100f);
        public NetworkVariable<float> MaxStamina = new NetworkVariable<float>(100f);

        public void ApplyDamage(float amount)
        {
            if (!IsServer) return;
            Health.Value = Mathf.Clamp(Health.Value - amount, 0, MaxHealth.Value);
        }

        public void ApplyHealing(float amount)
        {
            if (!IsServer) return;
            Health.Value = Mathf.Clamp(Health.Value + amount, 0, MaxHealth.Value);
        }

        public bool ConsumeMana(float amount)
        {
            if (!IsServer) return false;
            if (Mana.Value >= amount)
            {
                Mana.Value -= amount;
                return true;
            }
            return false;
        }
    }
}
