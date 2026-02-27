using System;
using System.Collections.Generic;
using UnityEngine;

namespace ByteWar.Survival
{
    /// <summary>
    /// Deterministic itemId -> GearVisualProfile lookup used by replicated loadout IDs.
    /// </summary>
    [CreateAssetMenu(fileName = "NewGearVisualProfileRegistry", menuName = "ByteWar/Survival/GearVisualProfileRegistry")]
    public sealed class GearVisualProfileRegistry : ScriptableObject
    {
        [SerializeField] private List<GearVisualProfile> _profiles = new();

        private readonly Dictionary<string, GearVisualProfile> _profilesByItemId = new(StringComparer.Ordinal);
        private bool _indexDirty = true;

        public IReadOnlyList<GearVisualProfile> Profiles => _profiles;

        private void OnEnable()
        {
            _indexDirty = true;
        }

        private void OnValidate()
        {
            _indexDirty = true;
        }

        public bool TryResolve(string itemId, out GearVisualProfile profile)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            RebuildIndexIfNeeded();
            return _profilesByItemId.TryGetValue(itemId, out profile) && profile != null;
        }

        internal void SetProfilesForTests(List<GearVisualProfile> profiles)
        {
            _profiles = profiles ?? new List<GearVisualProfile>();
            _indexDirty = true;
        }

        private void RebuildIndexIfNeeded()
        {
            if (!_indexDirty)
                return;

            _profilesByItemId.Clear();

            for (int i = 0; i < _profiles.Count; i++)
            {
                var profile = _profiles[i];
                if (profile == null)
                    continue;

                string itemId = (profile.ItemId ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(itemId))
                {
                    Debug.LogWarning($"[GearVisualProfileRegistry] Skipping profile '{profile.name}' with empty itemId.");
                    continue;
                }

                if (_profilesByItemId.ContainsKey(itemId))
                {
                    Debug.LogWarning($"[GearVisualProfileRegistry] Duplicate itemId '{itemId}' encountered. Keeping first profile.");
                    continue;
                }

                _profilesByItemId.Add(itemId, profile);
            }

            _indexDirty = false;
        }
    }
}