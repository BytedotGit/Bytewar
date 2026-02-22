using UnityEngine;
using Unity.Netcode;

namespace ByteWar.Survival
{
    public class SurvivalStats : NetworkBehaviour
    {
        public NetworkVariable<float> Hunger = new NetworkVariable<float>(100f);
        public NetworkVariable<float> MaxHunger = new NetworkVariable<float>(100f);
        [SerializeField] private float _hungerDecayRate = 1f;
        public float HungerDecayRate => _hungerDecayRate;

        private void Update()
        {
            if (!IsSpawned || !IsServer) return;

            if (Hunger.Value > 0)
            {
                Hunger.Value -= HungerDecayRate * Time.deltaTime;
            }
            else
            {
                // Apply starvation damage
                // GetComponent<AttributeSet>().ApplyDamage(1f * Time.deltaTime);
            }
        }

        public void EatFood(float amount)
        {
            if (!IsServer) return;
            Hunger.Value = Mathf.Clamp(Hunger.Value + amount, 0, MaxHunger.Value);
        }
    }
}
