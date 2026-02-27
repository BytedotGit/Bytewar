using System.Collections.Generic;
using UnityEngine;

namespace ByteWar.Survival
{
    [System.Serializable]
    public struct DestructibleRuntimeState
    {
        [SerializeField] private DestructibleStateType _state;
        [SerializeField] private float _currentHealth;
        [SerializeField] private DamageType _lastDamageType;

        public DestructibleStateType State { get => _state; internal set => _state = value; }
        public float CurrentHealth { get => _currentHealth; internal set => _currentHealth = value; }
        public DamageType LastDamageType { get => _lastDamageType; internal set => _lastDamageType = value; }
    }

    public static class DamageResolver
    {
        public static float ResolveEffectiveDamage(float baseDamage, DamageType damageType, DestructibleProfile profile)
        {
            float clampedBaseDamage = Mathf.Max(0f, baseDamage);
            if (profile == null)
                return clampedBaseDamage;

            float multiplier = Mathf.Max(0f, profile.GetDamageMultiplier(damageType));
            return clampedBaseDamage * multiplier;
        }
    }

    public static class DestructibleStateEvaluator
    {
        public static DestructibleStateType ResolveState(float currentHealth, float maxHealth, IReadOnlyList<DestructibleStateThreshold> thresholds)
        {
            if (currentHealth <= 0f)
                return DestructibleStateType.Destroyed;

            float normalizedHealth = maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
            DestructibleStateType resolvedState = DestructibleStateType.Intact;
            float bestThreshold = 2f;

            if (thresholds == null)
                return resolvedState;

            for (int i = 0; i < thresholds.Count; i++)
            {
                var threshold = thresholds[i];
                if (threshold.State == DestructibleStateType.Destroyed)
                    continue;

                float thresholdValue = Mathf.Clamp01(threshold.NormalizedHealthThreshold);
                if (normalizedHealth > thresholdValue)
                    continue;

                if (thresholdValue < bestThreshold)
                {
                    bestThreshold = thresholdValue;
                    resolvedState = threshold.State;
                }
            }

            return resolvedState;
        }

        public static DestructibleRuntimeState BuildRuntimeState(
            float currentHealth,
            float maxHealth,
            DamageType lastDamageType,
            IReadOnlyList<DestructibleStateThreshold> thresholds)
        {
            return new DestructibleRuntimeState
            {
                CurrentHealth = currentHealth,
                LastDamageType = lastDamageType,
                State = ResolveState(currentHealth, maxHealth, thresholds),
            };
        }
    }
}