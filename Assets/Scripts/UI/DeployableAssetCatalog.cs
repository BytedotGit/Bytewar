using System;
using System.Collections.Generic;
using UnityEngine;

namespace ByteWar.UI
{
    [CreateAssetMenu(
        fileName = "DeployableAssetCatalog",
        menuName = "ByteWar/Dev/Deployable Asset Catalog")]
    public sealed class DeployableAssetCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string _displayName;
            [SerializeField] private string _resourcePath;
            [SerializeField] private string _previewResourcePath;

            public Entry(string displayName, string resourcePath, string previewResourcePath)
            {
                _displayName = displayName ?? string.Empty;
                _resourcePath = resourcePath ?? string.Empty;
                _previewResourcePath = previewResourcePath ?? string.Empty;
            }

            public string DisplayName => _displayName;
            public string ResourcePath => _resourcePath;
            public string PreviewResourcePath => _previewResourcePath;
        }

        [SerializeField] private List<Entry> _entries = new();

        public IReadOnlyList<Entry> Entries => _entries;

        public void SetEntries(List<Entry> entries)
        {
            _entries = entries ?? new List<Entry>();
        }
    }
}
