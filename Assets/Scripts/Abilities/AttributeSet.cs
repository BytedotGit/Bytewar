using UnityEngine;
using Unity.Netcode;

namespace ByteWar.Abilities
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
            // Server-authoritative when NGO is active. In offline contexts (EditMode tests or
            // local non-networked usage), allow logic to run without a NetworkManager.
            if (!IsServer && NetworkManager.Singleton != null) return;
            Health.Value = Mathf.Clamp(Health.Value - amount, 0, MaxHealth.Value);
        }

        public void ApplyHealing(float amount)
        {
            if (!IsServer && NetworkManager.Singleton != null) return;
            Health.Value = Mathf.Clamp(Health.Value + amount, 0, MaxHealth.Value);
        }

        public bool ConsumeMana(float amount)
        {
            if (!IsServer && NetworkManager.Singleton != null) return false;
            if (Mana.Value >= amount)
            {
                Mana.Value -= amount;
                return true;
            }
            return false;
        }
    }
}
