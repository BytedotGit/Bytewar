using UnityEngine;
using System;

namespace ByteWar.Core
{
    /// <summary>
    /// Non-networked singleton that plays positional or 2-D SFX clips.
    /// Subscribes to <see cref="GameEventBus"/> for automatic gameplay sound triggers.
    ///
    /// Clip slots are populated at runtime from <c>Resources/SFX/</c> by
    /// <see cref="ByteWar.Editor.AudioClipGenerator"/>; if a clip is not found the call
    /// degrades gracefully without throwing.
    ///
    /// Uses two <see cref="AudioSource"/> components:
    ///   - <c>_2DSource</c>: UI / non-positional sounds (spatialBlend = 0)
    ///   - a small pool of spatial sources for overlapping 3D sounds
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────────

        public static AudioManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("SFX Clips (auto-loaded from Resources/SFX/)")]
        [SerializeField] private AudioClip _footstepClip;
        [SerializeField] private AudioClip _meleeHitClip;
        [SerializeField] private AudioClip _fireballCastClip;
        [SerializeField] private AudioClip _fireballImpactClip;
        [SerializeField] private AudioClip _itemPickupClip;
        [SerializeField] private AudioClip _buildingPlaceClip;
        [SerializeField] private AudioClip _uiClickClip;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource _2DSource;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private AudioSource[] _spatialPool;
        private int _poolIndex;

        // Exposed for testing
        internal SFXType LastPlayedSFX { get; private set; } = (SFXType)(-1);
        internal int FootstepCount { get; private set; }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[AudioManager] Duplicate AudioManager destroyed.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildSpatialPool();
            LoadClipsFromResources();
            SubscribeToEventBus();

            Debug.Log("[AudioManager] Singleton initialised with spatial pool size=3.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                UnsubscribeFromEventBus();
            }
        }

        // ── Clip loading ─────────────────────────────────────────────────────────

        private void LoadClipsFromResources()
        {
            // Only load from disk if the inspector slot is un-assigned.
            // This allows the editor to override with hand-crafted clips later.
            _footstepClip      ??= Resources.Load<AudioClip>("SFX/Footstep");
            _meleeHitClip      ??= Resources.Load<AudioClip>("SFX/MeleeHit");
            _fireballCastClip  ??= Resources.Load<AudioClip>("SFX/FireballCast");
            _fireballImpactClip ??= Resources.Load<AudioClip>("SFX/FireballImpact");
            _itemPickupClip    ??= Resources.Load<AudioClip>("SFX/ItemPickup");
            _buildingPlaceClip ??= Resources.Load<AudioClip>("SFX/BuildingPlace");
            _uiClickClip       ??= Resources.Load<AudioClip>("SFX/UIClick");

            Debug.Log($"[AudioManager] Clips loaded: footstep={_footstepClip != null} melee={_meleeHitClip != null} " +
                      $"fbCast={_fireballCastClip != null} fbImpact={_fireballImpactClip != null} " +
                      $"pickup={_itemPickupClip != null} building={_buildingPlaceClip != null} ui={_uiClickClip != null}");
        }

        // ── AudioSource pool ─────────────────────────────────────────────────────

        private void BuildSpatialPool()
        {
            const int PoolSize = 3;
            _spatialPool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"SpatialAudioSource_{i}");
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.maxDistance = 50f;
                src.playOnAwake = false;
                _spatialPool[i] = src;
            }

            // Flat 2D source for UI and footsteps (spatialBlend=0)
            if (_2DSource == null)
            {
                var go2D = new GameObject("2DAudioSource");
                go2D.transform.SetParent(transform);
                _2DSource = go2D.AddComponent<AudioSource>();
                _2DSource.spatialBlend = 0f;
                _2DSource.playOnAwake = false;
            }
        }

        // ── Event bus ────────────────────────────────────────────────────────────

        private void SubscribeToEventBus()
        {
            GameEventBus.EnemyDied.OnEvent    += OnEnemyDied;
            GameEventBus.ItemAdded.OnEvent     += OnItemAdded;
            GameEventBus.BuildingPlaced.OnEvent += OnBuildingPlaced;
        }

        private void UnsubscribeFromEventBus()
        {
            GameEventBus.EnemyDied.OnEvent    -= OnEnemyDied;
            GameEventBus.ItemAdded.OnEvent     -= OnItemAdded;
            GameEventBus.BuildingPlaced.OnEvent -= OnBuildingPlaced;
        }

        // NOTE: event payloads carry Vector3 world positions where available.
        private void OnEnemyDied(EnemyDiedEvent args)
        {
            PlaySFX(SFXType.MeleeHit, args.Position);
        }

        private void OnItemAdded(ItemEvent args)
        {
            PlaySFX(SFXType.ItemPickup);
        }

        private void OnBuildingPlaced(BuildingPlacedEvent args)
        {
            PlaySFX(SFXType.BuildingPlace, args.Position);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Play a sound effect. <paramref name="position"/> is only used for 3D spatial clips.
        /// Footstep and UIClick play as 2D regardless of position.
        /// </summary>
        public void PlaySFX(SFXType type, Vector3 position = default)
        {
            AudioClip clip = GetClip(type);
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] No clip assigned for SFXType={type}. Skipping playback.");
                return;
            }

            LastPlayedSFX = type;

            if (type == SFXType.Footstep || type == SFXType.UIClick)
            {
                _2DSource.PlayOneShot(clip, type == SFXType.Footstep ? 0.55f : 0.8f);
                if (type == SFXType.Footstep) FootstepCount++;
                Debug.Log($"[AudioManager] 2D SFX: {type}");
            }
            else
            {
                // Get next available spatial source from pool
                AudioSource src = GetNextSpatialSource();
                src.transform.position = position;
                src.PlayOneShot(clip);
                Debug.Log($"[AudioManager] Spatial SFX: {type} at {position}");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private AudioClip GetClip(SFXType type) => type switch
        {
            SFXType.Footstep       => _footstepClip,
            SFXType.MeleeHit       => _meleeHitClip,
            SFXType.FireballCast   => _fireballCastClip,
            SFXType.FireballImpact => _fireballImpactClip,
            SFXType.ItemPickup     => _itemPickupClip,
            SFXType.BuildingPlace  => _buildingPlaceClip,
            SFXType.UIClick        => _uiClickClip,
            _                      => null,
        };

        private AudioSource GetNextSpatialSource()
        {
            AudioSource src = _spatialPool[_poolIndex];
            _poolIndex = (_poolIndex + 1) % _spatialPool.Length;
            return src;
        }

#if UNITY_EDITOR
        /// <summary>Allows <see cref="ByteWar.Editor.AudioClipGenerator"/> to wire clips at generation time.</summary>
        public void SetClips(
            AudioClip footstep, AudioClip melee, AudioClip fbCast,
            AudioClip fbImpact, AudioClip pickup, AudioClip building, AudioClip ui)
        {
            _footstepClip       = footstep;
            _meleeHitClip       = melee;
            _fireballCastClip   = fbCast;
            _fireballImpactClip = fbImpact;
            _itemPickupClip     = pickup;
            _buildingPlaceClip  = building;
            _uiClickClip        = ui;
        }
#endif
    }
}
