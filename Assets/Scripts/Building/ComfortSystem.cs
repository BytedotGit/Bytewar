using UnityEngine;
using System.Collections.Generic;

namespace ByteWar.Building
{
    /// <summary>
    /// Valheim-style comfort system. Scans nearby building pieces within a shelter radius
    /// and calculates a comfort level based on unique ComfortGroup contributions.
    /// Only one piece per ComfortGroup counts (no stacking multiple fireplaces).
    /// </summary>
    public class ComfortSystem : MonoBehaviour
    {
        [SerializeField] private float _comfortRadius = 10f;
        [SerializeField] private float _updateInterval = 2f;
        [SerializeField] private float _baseComfort = 1f;

        private float _currentComfort;
        private float _nextUpdateTime;
        private bool _isSheltered;
        private readonly Collider[] _searchBuffer = new Collider[64];
        private readonly Dictionary<ComfortGroup, float> _groupContributions = new();

        /// <summary>Current comfort level (base + unique group contributions).</summary>
        public float CurrentComfort => _currentComfort;

        /// <summary>Whether the player is considered sheltered (has roof + walls nearby).</summary>
        public bool IsSheltered => _isSheltered;

        /// <summary>Per-group comfort contributions for UI display.</summary>
        public IReadOnlyDictionary<ComfortGroup, float> GroupContributions => _groupContributions;

        private void Update()
        {
            if (Time.time < _nextUpdateTime) return;
            _nextUpdateTime = Time.time + _updateInterval;
            RecalculateComfort();
        }

        /// <summary>
        /// Recalculates comfort by scanning nearby building pieces.
        /// Only the highest-value piece per ComfortGroup counts.
        /// </summary>
        public void RecalculateComfort()
        {
            _groupContributions.Clear();
            float totalComfort = _baseComfort;

            // Check shelter (roof above)
            _isSheltered = CheckShelter();
            if (!_isSheltered)
            {
                _currentComfort = _baseComfort;
                return;
            }

            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _comfortRadius, _searchBuffer);

            for (int i = 0; i < count; i++)
            {
                if (_searchBuffer[i] == null) continue;
                var piece = _searchBuffer[i].GetComponentInParent<BuildingPiece>();
                if (piece == null || piece.ComfortValue <= 0f) continue;

                var group = piece.ComfortGroup;
                if (group == ComfortGroup.None)
                {
                    // Non-grouped comfort always stacks
                    totalComfort += piece.ComfortValue;
                    continue;
                }

                // Only keep the highest value per group
                if (_groupContributions.TryGetValue(group, out float existing))
                {
                    if (piece.ComfortValue > existing)
                        _groupContributions[group] = piece.ComfortValue;
                }
                else
                {
                    _groupContributions[group] = piece.ComfortValue;
                }
            }

            // Sum group contributions
            foreach (var contribution in _groupContributions.Values)
                totalComfort += contribution;

            _currentComfort = totalComfort;
            Debug.Log($"[ComfortSystem] Comfort={_currentComfort:0.0} sheltered={_isSheltered} groups={_groupContributions.Count}");
        }

        /// <summary>
        /// Checks if the player is sheltered by raycasting upward for a roof piece.
        /// </summary>
        private bool CheckShelter()
        {
            if (Physics.Raycast(transform.position, Vector3.up, out RaycastHit hit, 20f))
            {
                var piece = hit.collider.GetComponentInParent<BuildingPiece>();
                if (piece != null &&
                    (piece.PieceType == BuildingPieceType.Roof26 ||
                     piece.PieceType == BuildingPieceType.Floor))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
