using System;
using System.Collections.Generic;
using UnityEngine;

namespace ByteWar.Editor
{
    [Serializable]
    public sealed class LargeTreeVariationProfileEntry
    {
        [SerializeField] private string _variationName = string.Empty;
        [SerializeField] private GameObject _prefab;
        [SerializeField, Min(0f)] private float _weight = 1f;

        public string VariationName => _variationName;
        public GameObject Prefab => _prefab;
        public float Weight => _weight;

        public LargeTreeVariationProfileEntry()
        {
        }

        public LargeTreeVariationProfileEntry(string variationName, GameObject prefab, float weight)
        {
            _variationName = variationName ?? string.Empty;
            _prefab = prefab;
            _weight = Mathf.Max(0f, weight);
        }
    }

    public sealed class LargeTreeVariationProfile : ScriptableObject
    {
        [SerializeField] private List<LargeTreeVariationProfileEntry> _entries = new List<LargeTreeVariationProfileEntry>();

        public IReadOnlyList<LargeTreeVariationProfileEntry> Entries => _entries;

        public void SetEntries(List<LargeTreeVariationProfileEntry> entries)
        {
            _entries = entries ?? new List<LargeTreeVariationProfileEntry>();
        }
    }
}
