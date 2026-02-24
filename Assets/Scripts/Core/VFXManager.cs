using UnityEngine;
using Unity.Netcode;

namespace ByteWar.Core
{
    /// <summary>
    /// Singleton NetworkBehaviour that plays particle VFX on all clients.
    /// The server holds the authoritative source-of-truth for VFX triggers; clients
    /// receive a ClientRpc and instantiate a self-destructing particle prefab locally.
    /// 
    /// Prefab slots are wired by <see cref="ByteWar.Editor.VFXPrefabGenerator"/> at build time.
    /// </summary>
    public class VFXManager : NetworkBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────────

        public static VFXManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("VFX Prefabs (auto-wired by VFXPrefabGenerator)")]
        [SerializeField] private GameObject _fireballMuzzlePrefab;
        [SerializeField] private GameObject _fireballImpactPrefab;
        [SerializeField] private GameObject _gatherHitPrefab;
        [SerializeField] private GameObject _resourceDeathPrefab;
        [SerializeField] private GameObject _buildingPlacePrefab;
        [SerializeField] private GameObject _itemPickupPrefab;
        [SerializeField] private GameObject _cleaveHitPrefab;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[VFXManager] Duplicate VFXManager destroyed.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Debug.Log("[VFXManager] Singleton initialised.");
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Plays the requested VFX at <paramref name="worldPosition"/> on ALL clients.
        /// Must be called on the server (inside a ServerRpc or other server-only path).
        /// </summary>
        public void PlayEffect(VFXType type, Vector3 worldPosition)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"[VFXManager] PlayEffect called on a non-server instance. VFXType={type}");
                return;
            }

            Debug.Log($"[VFXManager] Broadcasting VFX: {type} at {worldPosition}");
            PlayEffectClientRpc((int)type, worldPosition);
        }

        // ── ClientRpc ─────────────────────────────────────────────────────────────

        [ClientRpc]
        private void PlayEffectClientRpc(int typeIndex, Vector3 worldPosition)
        {
            PlayEffectLocal((VFXType)typeIndex, worldPosition);
        }

        /// <summary>
        /// Instantiates the VFX prefab locally. Safe to call from any context
        /// (inside a ClientRpc, from editor tools, or from test code).
        /// Does NOT broadcast to remote clients — use <see cref="PlayEffect"/> for that.
        /// </summary>
        public void PlayEffectLocal(VFXType type, Vector3 worldPosition)
        {
            GameObject prefab = GetPrefab(type);
            if (prefab == null)
            {
                Debug.LogWarning($"[VFXManager] No prefab assigned for VFXType={type}. Skipping.");
                return;
            }

            Debug.Log($"[VFXManager] PlayEffectLocal: {type} at {worldPosition}");
            Instantiate(prefab, worldPosition, Quaternion.identity);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private GameObject GetPrefab(VFXType type) => type switch
        {
            VFXType.FireballMuzzle  => _fireballMuzzlePrefab,
            VFXType.FireballImpact  => _fireballImpactPrefab,
            VFXType.GatherHit       => _gatherHitPrefab,
            VFXType.ResourceDeath   => _resourceDeathPrefab,
            VFXType.BuildingPlace   => _buildingPlacePrefab,
            VFXType.ItemPickup      => _itemPickupPrefab,
            VFXType.CleaveHit       => _cleaveHitPrefab,
            _                       => null,
        };

        /// <summary>Inspector/test accessor — returns whether all 6 prefab slots are assigned.</summary>
        public bool AllPrefabsAssigned =>
            _fireballMuzzlePrefab  != null &&
            _fireballImpactPrefab  != null &&
            _gatherHitPrefab       != null &&
            _resourceDeathPrefab   != null &&
            _buildingPlacePrefab   != null &&
            _itemPickupPrefab      != null &&
            _cleaveHitPrefab       != null;

        // ── Editor helpers (called by VFXPrefabGenerator) ─────────────────────────

#if UNITY_EDITOR
        public void SetPrefabs(
            GameObject muzzle, GameObject impact,
            GameObject gather, GameObject resDeath,
            GameObject building, GameObject pickup,
            GameObject cleave = null)
        {
            _fireballMuzzlePrefab  = muzzle;
            _fireballImpactPrefab  = impact;
            _gatherHitPrefab       = gather;
            _resourceDeathPrefab   = resDeath;
            _buildingPlacePrefab   = building;
            _itemPickupPrefab      = pickup;
            _cleaveHitPrefab       = cleave;
        }
#endif
    }
}
