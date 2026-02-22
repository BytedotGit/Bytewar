using UnityEngine;
using Unity.Netcode;
using ByteWar.Abilities;
using ByteWar.Survival;
using ByteWar.Core;

namespace ByteWar.Networking
{
    public enum PlayerVisualMode
    {
        Unknown = 0,
        Mixamo = 1,
        HumanoidFallback = 2,
        PrimitiveFallback = 3,
        OtherSkinned = 4,
    }

    /// <summary>
    /// Orchestrator for the local player. Owns camera setup, spawn logic, and diagnostics.
    /// Movement/abilities are handled by <see cref="PlayerMovement"/>.
    /// Visual grounding and animator repair are handled by <see cref="PlayerVisualSetup"/>.
    /// </summary>
    [RequireComponent(typeof(AbilitySystemComponent))]
    [RequireComponent(typeof(SurvivalStats))]
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerVisualSetup))]
    public class NetworkPlayer : NetworkBehaviour
    {
        public struct VisualDiagnostics
        {
            public int SkinnedMeshRendererCount;
            public int RendererCount;
            public bool HasMixamoRig;
            public string VisualRootChildName;
        }

        // ── References ────────────────────────────────────────────────────────────
        public AbilitySystemComponent AbilitySystem { get; private set; }
        public SurvivalStats SurvivalStats { get; private set; }

        private PlayerInputHandler _inputHandler;
        private Animator _animator;
        private CharacterController _cc;
        private ThirdPersonCamera _camController;
        private PlayerVisualSetup _visualSetup;

        [Header("Generated Prefab Diagnostics")]
        [Tooltip("Set by PrefabGenerator so runtime/tests can prove which visual branch was baked (Mixamo vs fallback).")]
        [SerializeField] private PlayerVisualMode _generatedVisualModeStamp = PlayerVisualMode.Unknown;

        public PlayerVisualMode GeneratedVisualModeStamp => _generatedVisualModeStamp;

        public void SetGeneratedVisualModeStamp(PlayerVisualMode mode)
        {
            _generatedVisualModeStamp = mode;
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────────
        private void Awake()
        {
            AbilitySystem = GetComponent<AbilitySystemComponent>();
            SurvivalStats = GetComponent<SurvivalStats>();
            _inputHandler = GetComponent<PlayerInputHandler>();
            _animator = GetComponent<Animator>();
            _cc = GetComponent<CharacterController>();
            _visualSetup = GetComponent<PlayerVisualSetup>();
            Debug.Log($"[NetworkPlayer] Awake on {gameObject.name}.");
        }

        public override void OnNetworkSpawn()
        {
            // Register in player registry for all instances (server tracks all players)
            PlayerRegistry.Register(transform);

            if (!IsOwner)
            {
                if (_inputHandler != null) _inputHandler.enabled = false;
                return;
            }

            Debug.Log("[NetworkPlayer] Local player spawned — setting up camera.");

            if (_inputHandler != null)
            {
                if (!_inputHandler.enabled)
                {
                    Debug.LogWarning("[NetworkPlayer] PlayerInputHandler was disabled on owner — re-enabling.");
                    _inputHandler.enabled = true;
                }
                _inputHandler.EnsureActionsEnabled("NetworkPlayer OnNetworkSpawn (owner)");
            }

            // Spawn on ground surface at world origin. Prefer raycast (greybox/any collider);
            // fall back to Terrain if present; otherwise stay at origin.
            Vector3 spawnPos = new Vector3(0f, 2f, 0f);
            if (Physics.Raycast(new Vector3(0f, 50f, 0f), Vector3.down, out RaycastHit spawnHit, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                spawnPos = spawnHit.point + Vector3.up * 1.5f;
                Debug.Log($"[NetworkPlayer] Spawn via raycast: {spawnPos} (hit={spawnHit.collider.name})");
            }
            else if (Terrain.activeTerrain != null)
            {
                Terrain t = Terrain.activeTerrain;
                Vector3 terrainCenter = t.transform.position
                    + new Vector3(t.terrainData.size.x * 0.5f, 0f, t.terrainData.size.z * 0.5f);
                float groundY = t.SampleHeight(terrainCenter);
                spawnPos = new Vector3(terrainCenter.x, groundY + t.transform.position.y + 2f, terrainCenter.z);
                Debug.Log($"[NetworkPlayer] Spawn via Terrain center: {spawnPos}");
            }
            else
            {
                Debug.LogWarning("[NetworkPlayer] No ground collider or terrain found! Spawning at default position.");
            }

            // Disable CharacterController during teleport — otherwise it blocks position changes
            if (_cc != null) _cc.enabled = false;
            transform.position = spawnPos;
            if (_cc != null) _cc.enabled = true;

            // Put player on its own layer so the camera SphereCast ignores them
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            SetChildLayers(transform, gameObject.layer);

            SetupCamera();

            // Visual diagnostics (one-time): make Mixamo vs fallback provable in Player.log.
            PlayerVisualMode actual = DetectActualVisualMode(out VisualDiagnostics diag);
            Debug.Log($"[NetworkPlayer] VisualMode: stamp={_generatedVisualModeStamp} actual={actual} child='{diag.VisualRootChildName}' skinned={diag.SkinnedMeshRendererCount} renderers={diag.RendererCount} mixamoRig={diag.HasMixamoRig} netObj={NetworkObjectId} ownerClient={OwnerClientId}");

            if (_animator != null)
            {
                string controllerName = _animator.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.name : "(null)";
                string avatarName = _animator.avatar != null ? _animator.avatar.name : "(null)";
                int clipCount = 0;
                try { clipCount = _animator.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.animationClips.Length : 0; }
                catch { /* ignore */ }

                Debug.Log($"[NetworkPlayer] Animator wiring: enabled={_animator.enabled} isHuman={_animator.isHuman} controller='{controllerName}' avatar='{avatarName}' clips={clipCount}");
            }
            if (_generatedVisualModeStamp != PlayerVisualMode.Unknown && actual != _generatedVisualModeStamp)
            {
                Debug.LogWarning($"[NetworkPlayer] VisualMode mismatch: stamp={_generatedVisualModeStamp} actual={actual}");
            }
        }

        public override void OnNetworkDespawn()
        {
            PlayerRegistry.Unregister(transform);
        }

        // ── Camera Setup ──────────────────────────────────────────────────────────

        internal void SetupCamera()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogError("[NetworkPlayer] No Main Camera found in scene!");
                return;
            }

            _camController = mainCam.GetComponent<ThirdPersonCamera>();
            if (_camController == null)
            {
                _camController = mainCam.gameObject.AddComponent<ThirdPersonCamera>();
                Debug.Log("[NetworkPlayer] ThirdPersonCamera added to Main Camera at runtime.");
            }

            // Use CameraTarget child (head height) when available
            Transform pivot = transform.Find("CameraTarget");
            if (pivot != null)
            {
                float localY = ComputeCameraTargetLocalY();
                pivot.localPosition = new Vector3(0f, localY, 0f);

                _camController.SetTarget(pivot, pivotHeight: 0.20f);
                Debug.Log($"[NetworkPlayer] CameraTarget set to localY={localY:0.00}; camera pivotHeight=0.20");
            }
            else
            {
                _camController.SetTarget(transform, pivotHeight: 1.40f);
                Debug.Log("[NetworkPlayer] CameraTarget missing; using root target with pivotHeight=1.40");
            }
        }

        private float ComputeCameraTargetLocalY()
        {
            if (_animator != null && _animator.isHuman)
            {
                Transform bone = _animator.GetBoneTransform(HumanBodyBones.UpperChest)
                                 ?? _animator.GetBoneTransform(HumanBodyBones.Chest)
                                 ?? _animator.GetBoneTransform(HumanBodyBones.Head);
                if (bone != null)
                {
                    float y = bone.position.y - transform.position.y;
                    float controllerHeight = _cc != null ? _cc.height : 2f;
                    float min = Mathf.Max(0.8f, controllerHeight * 0.45f);
                    float max = Mathf.Min(1.8f, controllerHeight * 0.95f);
                    return Mathf.Clamp(y, min, max);
                }
            }

            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    b.Encapsulate(renderers[i].bounds);

                float y = (b.center.y - transform.position.y) + 0.15f;
                return Mathf.Clamp(y, 0.9f, 1.6f);
            }

            return 1.25f;
        }

        private static void SetChildLayers(Transform parent, int layer)
        {
            foreach (Transform child in parent)
            {
                child.gameObject.layer = layer;
                SetChildLayers(child, layer);
            }
        }

        // ── Visual mode detection (delegates to PlayerVisualSetup) ────────────────

        public PlayerVisualMode DetectActualVisualMode(out VisualDiagnostics diagnostics)
        {
            // Lazy-acquire in case called before Awake (e.g. EditMode tests)
            if (_visualSetup == null)
                _visualSetup = GetComponent<PlayerVisualSetup>();

            if (_visualSetup != null)
                return _visualSetup.DetectActualVisualMode(out diagnostics);

            // Fallback when PlayerVisualSetup is missing (e.g. minimal test prefabs)
            diagnostics = default;
            return PlayerVisualMode.PrimitiveFallback;
        }
    }
}
