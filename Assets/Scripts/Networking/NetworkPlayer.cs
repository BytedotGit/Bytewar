using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using SurvivalRPG.Abilities;
using SurvivalRPG.Survival;
using SurvivalRPG.Core;

namespace SurvivalRPG.Networking
{
    public enum PlayerVisualMode
    {
        Unknown = 0,
        Mixamo = 1,
        HumanoidFallback = 2,
        PrimitiveFallback = 3,
        OtherSkinned = 4,
    }

    [RequireComponent(typeof(AbilitySystemComponent))]
    [RequireComponent(typeof(SurvivalStats))]
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(CharacterController))]
    public class NetworkPlayer : NetworkBehaviour
    {
        private const string ResourcesAnimatorControllerPath = "Generated/PlayerAnimatorController";

        public struct VisualDiagnostics
        {
            public int SkinnedMeshRendererCount;
            public int RendererCount;
            public bool HasMixamoRig;
            public string VisualRootChildName;
        }

        // ── Movement constants ────────────────────────────────────────────────────
        private const float MoveSpeed = 6f;
        private const float TurnSpeed = 14f;
        private const float Gravity = -18f;
        private const float JumpForce = 6f;

        // ── References ────────────────────────────────────────────────────────────
        public AbilitySystemComponent AbilitySystem { get; private set; }
        public SurvivalStats SurvivalStats { get; private set; }

        private PlayerInputHandler _inputHandler;
        private Animator _animator;
        private ClientNetworkAnimator _netAnimator;
        private CharacterController _cc;
        private ThirdPersonCamera _camController;

        private float _verticalVelocity;
        private bool _initialized;
        private float _nextMoveLogTime;

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
            _netAnimator = GetComponent<ClientNetworkAnimator>();
            _cc = GetComponent<CharacterController>();
            Debug.Log($"[NetworkPlayer] Awake on {gameObject.name}.");

            // Runtime hardening: in some build scenarios the controller reference can be null.
            // This is a one-time repair (no per-frame work).
            EnsureAnimatorControllerAssigned("Awake");
        }

        public override void OnNetworkSpawn()
        {
            // Ensure all clients have an AnimatorController, not just the owner.
            EnsureAnimatorControllerAssigned("OnNetworkSpawn");

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

            // Spawn on terrain surface at world-space center of the terrain
            if (Terrain.activeTerrain != null)
            {
                Terrain t = Terrain.activeTerrain;
                // Center of terrain in world space
                Vector3 terrainCenter = t.transform.position
                    + new Vector3(t.terrainData.size.x * 0.5f, 0f, t.terrainData.size.z * 0.5f);
                float groundY = t.SampleHeight(terrainCenter);
                Vector3 spawnPos = new Vector3(terrainCenter.x, groundY + t.transform.position.y + 2f, terrainCenter.z);

                // Disable CharacterController during teleport — otherwise it blocks position changes
                if (_cc != null) _cc.enabled = false;
                transform.position = spawnPos;
                if (_cc != null) _cc.enabled = true;

                Debug.Log($"[NetworkPlayer] Placed at terrain center: {spawnPos} (terrain pos={t.transform.position}, size={t.terrainData.size})");
            }
            else
            {
                Debug.LogWarning("[NetworkPlayer] No active terrain found! Player stays at origin.");
            }

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
                Debug.LogWarning($"[NetworkPlayer] VisualMode mismatch: stamp={_generatedVisualModeStamp} actual={actual} (this indicates stale generated prefabs, missing local assets, or an unexpected visual hierarchy).");
            }

            _initialized = true;
        }

        private void EnsureAnimatorControllerAssigned(string context)
        {
            if (_animator == null) return;
            if (_animator.runtimeAnimatorController != null) return;

            var controller = Resources.Load<RuntimeAnimatorController>(ResourcesAnimatorControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[NetworkPlayer] AnimatorController missing at runtime (context={context}). Resources.Load failed for '{ResourcesAnimatorControllerPath}'. netObj={NetworkObjectId} ownerClient={OwnerClientId}");
                return;
            }

            _animator.runtimeAnimatorController = controller;
            _animator.Rebind();
            _animator.Update(0f);
            Debug.LogWarning($"[NetworkPlayer] Repaired null AnimatorController via Resources (context={context}) -> '{controller.name}'. netObj={NetworkObjectId} ownerClient={OwnerClientId}");
        }

        public PlayerVisualMode DetectActualVisualMode(out VisualDiagnostics diagnostics)
        {
            Transform visualRoot = transform.Find("VisualRoot");
            string childName = (visualRoot != null && visualRoot.childCount > 0) ? visualRoot.GetChild(0).name : "(none)";

            var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            var skinned = GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);

            bool hasMixamoRig = false;
            var allTransforms = GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                var t = allTransforms[i];
                if (t != null && t.name.StartsWith("mixamorig:"))
                {
                    hasMixamoRig = true;
                    break;
                }
            }

            diagnostics = new VisualDiagnostics
            {
                VisualRootChildName = childName,
                RendererCount = renderers != null ? renderers.Length : 0,
                SkinnedMeshRendererCount = skinned != null ? skinned.Length : 0,
                HasMixamoRig = hasMixamoRig,
            };

            if (diagnostics.SkinnedMeshRendererCount > 0 && diagnostics.HasMixamoRig)
                return PlayerVisualMode.Mixamo;

            if (diagnostics.SkinnedMeshRendererCount > 0)
                return PlayerVisualMode.OtherSkinned;

            if (visualRoot != null && visualRoot.childCount > 0)
            {
                if (childName == "FallbackCapsule")
                    return PlayerVisualMode.PrimitiveFallback;
                return PlayerVisualMode.HumanoidFallback;
            }

            return PlayerVisualMode.PrimitiveFallback;
        }

        private void SetupCamera()
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

                // Hybrid strategy: CameraTarget carries the bulk of the height; camera adds only a small tweak.
                _camController.SetTarget(pivot, pivotHeight: 0.20f);
                Debug.Log($"[NetworkPlayer] CameraTarget set to localY={localY:0.00}; camera pivotHeight=0.20");
            }
            else
            {
                // Fallback for prefabs missing CameraTarget.
                _camController.SetTarget(transform, pivotHeight: 1.40f);
                Debug.Log("[NetworkPlayer] CameraTarget missing; using root target with pivotHeight=1.40");
            }
        }

        private float ComputeCameraTargetLocalY()
        {
            // Prefer humanoid bones when available for consistent framing across models.
            if (_animator != null && _animator.isHuman)
            {
                Transform bone = _animator.GetBoneTransform(HumanBodyBones.UpperChest)
                                 ?? _animator.GetBoneTransform(HumanBodyBones.Chest)
                                 ?? _animator.GetBoneTransform(HumanBodyBones.Head);
                if (bone != null)
                {
                    float y = bone.position.y - transform.position.y;

                    // Clamp into a reasonable band based on controller height (if present).
                    float controllerHeight = _cc != null ? _cc.height : 2f;
                    float min = Mathf.Max(0.8f, controllerHeight * 0.45f);
                    float max = Mathf.Min(1.8f, controllerHeight * 0.95f);
                    return Mathf.Clamp(y, min, max);
                }
            }

            // Fallback: renderer bounds.
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    b.Encapsulate(renderers[i].bounds);

                float y = (b.center.y - transform.position.y) + 0.15f;
                return Mathf.Clamp(y, 0.9f, 1.6f);
            }

            // Last resort.
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

        // ── Per-frame ─────────────────────────────────────────────────────────────
        private void Update()
        {
            if (!IsSpawned || !IsOwner || !_initialized) return;
            if (_cc == null) return;

            HandleMovement();
            HandleAbilities();
        }

        private void HandleMovement()
        {
            Vector2 input = _inputHandler.MovementInput;

            if (input.sqrMagnitude > 0.01f && Time.unscaledTime >= _nextMoveLogTime)
            {
                Debug.Log($"[NetworkPlayer] Moving: {input}");
                _nextMoveLogTime = Time.unscaledTime + 0.5f;
            }

            // WoW: holding both mouse buttons moves forward
            if (_camController != null &&
                _camController.IsLeftMouseHeld && _camController.IsRightMouseHeld)
            {
                input.y = Mathf.Max(input.y, 1f);
            }

            // Camera-relative move direction (flat)
            float camYaw = _camController != null ? _camController.CameraYaw : transform.eulerAngles.y;
            Quaternion camRot = Quaternion.Euler(0f, camYaw, 0f);
            Vector3 moveDir = camRot * new Vector3(input.x, 0f, input.y);
            bool isMoving = moveDir.magnitude > 0.05f;

            // Gravity and Jumping
            if (_cc.isGrounded)
            {
                _verticalVelocity = -1f;
                if (_inputHandler.ConsumeJump())
                {
                    _verticalVelocity = JumpForce;
                    if (_netAnimator != null) _netAnimator.SetTrigger("Jump");
                }
            }
            else
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }

            Vector3 velocity = moveDir.normalized * MoveSpeed;
            velocity.y = _verticalVelocity;
            _cc.Move(velocity * Time.deltaTime);

            // Rotation
            if (isMoving)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                                                        Time.deltaTime * TurnSpeed);
            }
            else if (_camController != null && _camController.IsRightMouseHeld)
            {
                // Standing + RMB: face camera yaw (WoW behaviour)
                Quaternion faceCam = Quaternion.Euler(0f, camYaw, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, faceCam,
                                                      Time.deltaTime * TurnSpeed);
            }

            if (_animator != null)
            {
                _animator.SetFloat("Speed", isMoving ? moveDir.magnitude * MoveSpeed : 0f);
                _animator.SetBool("IsGrounded", _cc.isGrounded);
            }
        }

        private void HandleAbilities()
        {
            if (AbilitySystem == null) return;

            if (_inputHandler.ConsumeCastSpell1())
            {
                Debug.Log("[NetworkPlayer] Spell 1 triggered.");
                bool ok = AbilitySystem.TryCastAbility(0, transform.position + transform.forward * 5f);
                if (ok)
                {
                    if (_netAnimator != null) _netAnimator.SetTrigger("Attack");
                }
                else
                {
                    Debug.Log("[NetworkPlayer] Spell 1 did not execute (empty slot / cooldown / insufficient mana).");
                }
            }

            if (_inputHandler.ConsumeCastSpell2())
            {
                Debug.Log("[NetworkPlayer] Spell 2 triggered.");
                bool ok = AbilitySystem.TryCastAbility(1, transform.position + transform.forward * 5f);
                if (ok)
                {
                    if (_netAnimator != null) _netAnimator.SetTrigger("Attack");
                }
                else
                {
                    Debug.Log("[NetworkPlayer] Spell 2 did not execute (empty slot / cooldown / insufficient mana).");
                }
            }
        }
    }
}
