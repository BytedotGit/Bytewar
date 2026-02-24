using UnityEngine;
using Unity.Netcode;
using ByteWar.Abilities;
using ByteWar.Building;
using ByteWar.Core;

namespace ByteWar.Networking
{
    /// <summary>
    /// Handles player movement, gravity, jumping, and ability input.
    /// Extracted from NetworkPlayer to keep single-responsibility.
    /// Must be on the same GameObject as NetworkPlayer.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : NetworkBehaviour
    {
        // ── Movement constants (from GameConstants SO with fallback defaults) ──────

        private PlayerInputHandler _inputHandler;
        private Animator _animator;
        private ClientNetworkAnimator _netAnimator;
        private CharacterController _cc;
        private ThirdPersonCamera _camController;
        private AbilitySystemComponent _abilitySystem;

        private float _verticalVelocity;
        private float _nextMoveLogTime;
        private bool _initialized;
        private bool _groundedAtMoveStart;
        private bool _wasGroundedLastFrame;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _inputHandler = GetComponent<PlayerInputHandler>();
            _animator = GetComponent<Animator>();
            _netAnimator = GetComponent<ClientNetworkAnimator>();
            _cc = GetComponent<CharacterController>();
            _abilitySystem = GetComponent<AbilitySystemComponent>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            Camera cam = Camera.main;
            if (cam != null)
                _camController = cam.GetComponent<ThirdPersonCamera>();

            _initialized = true;
            Debug.Log("[PlayerMovement] Initialized for local owner.");
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || !_initialized) return;
            if (_cc == null) return;

            // Re-acquire camera reference if needed (e.g. after scene loads)
            if (_camController == null)
            {
                Camera cam = Camera.main;
                if (cam != null)
                    _camController = cam.GetComponent<ThirdPersonCamera>();
            }

            HandleMovement();
            HandleAbilities();
        }

        private void LateUpdate()
        {
            if (!IsSpawned || !IsOwner || !_initialized) return;
            if (_cc == null || _inputHandler == null) return;

            // If the frame began airborne, discard any jump press that happened later
            // in the same frame. This prevents a mid-air Space press from being buffered
            // into a jump on the first grounded frame due to script execution order.
            if (!_groundedAtMoveStart)
                _inputHandler.ConsumeJump();
        }

        // ── Movement ──────────────────────────────────────────────────────────────

        private void HandleMovement()
        {
            Vector2 input = _inputHandler.MovementInput;

            _groundedAtMoveStart = _cc.isGrounded;

            if (input.sqrMagnitude > 0.01f && Time.unscaledTime >= _nextMoveLogTime)
            {
                Debug.Log($"[PlayerMovement] Moving: {input}");
                _nextMoveLogTime = Time.unscaledTime + 0.5f;
            }

            // WoW: holding both mouse buttons moves forward
            if (_camController != null &&
                _camController.IsLeftMouseHeld && _camController.IsRightMouseHeld)
            {
                input.y = Mathf.Max(input.y, 1f);
            }

            bool rmbHeld = _camController != null && _camController.IsRightMouseHeld;

            // WoW-style movement semantics:
            // - Without RMB: A/D turns (no strafe). Movement is relative to current facing.
            // - With RMB: A/D strafes. Movement is camera-relative, and facing follows camera yaw.
            float camYaw = _camController != null ? _camController.CameraYaw : transform.eulerAngles.y;

            if (!rmbHeld)
            {
                float yawDelta = input.x * GameConstants.GetKeyboardTurnSpeedDegPerSec() * Time.deltaTime;
                if (Mathf.Abs(yawDelta) > 0.0001f)
                {
                    Vector3 e = transform.eulerAngles;
                    transform.rotation = Quaternion.Euler(0f, e.y + yawDelta, 0f);
                }

                // No strafing when RMB is not held.
                input.x = 0f;
            }

            float basisYaw = rmbHeld ? camYaw : transform.eulerAngles.y;
            Quaternion basisRot = Quaternion.Euler(0f, basisYaw, 0f);
            Vector3 moveDir = basisRot * new Vector3(input.x, 0f, input.y);
            bool isMoving = moveDir.magnitude > 0.05f;

            // Gravity and Jumping
            if (_cc.isGrounded)
            {
                _verticalVelocity = -1f;

                // Do not allow a jump on the very first grounded frame after being airborne.
                // This prevents mid-air Space presses from being buffered into an auto-jump on landing.
                if (_wasGroundedLastFrame)
                {
                    if (_inputHandler.ConsumeJump())
                    {
                        _verticalVelocity = GameConstants.GetJumpForce();
                        if (_netAnimator != null) _netAnimator.SetTrigger("Jump");
                    }
                }
                else
                {
                    // Clear any queued jump input on landing.
                    _inputHandler.ConsumeJump();
                }
            }
            else
            {
                // Discard mid-air jump presses so they don't get buffered and trigger
                // an automatic jump on the next grounded frame.
                _inputHandler.ConsumeJump();
                _verticalVelocity += GameConstants.GetGravity() * Time.deltaTime;
            }

            Vector3 velocity = moveDir.normalized * GameConstants.GetMoveSpeed();
            velocity.y = _verticalVelocity;
            _cc.Move(velocity * Time.deltaTime);

            // CharacterController.isGrounded is updated by Move(); store for next frame.
            _wasGroundedLastFrame = _cc.isGrounded;

            // Rotation
            if (rmbHeld)
            {
                // RMB held: face travel direction when moving diagonally forward (W+A / W+D),
                // otherwise keep classic camera-yaw facing (strafing/backpedal).
                if (ShouldFaceMoveDirectionWhenRmbHeld(input, isMoving))
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                                                          Time.deltaTime * GameConstants.GetTurnSpeed());
                }
                else
                {
                    Quaternion faceCam = Quaternion.Euler(0f, camYaw, 0f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, faceCam,
                                                          Time.deltaTime * GameConstants.GetTurnSpeed());
                }
            }
            else if (isMoving)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                                                      Time.deltaTime * GameConstants.GetTurnSpeed());
            }

            if (_animator != null)
            {
                _animator.SetFloat("Speed", isMoving ? moveDir.magnitude * GameConstants.GetMoveSpeed() : 0f);
                _animator.SetBool("IsGrounded", _cc.isGrounded);
            }
        }

        internal static bool ShouldFaceMoveDirectionWhenRmbHeld(Vector2 input, bool isMoving)
        {
            // Requested behavior: W+A / W+D should turn to face the actual diagonal travel direction.
            // Preserve classic WoW: pure strafing and backpedal do not rotate to movement direction.
            return isMoving && input.y > 0.01f && Mathf.Abs(input.x) > 0.01f;
        }

        // ── Abilities ─────────────────────────────────────────────────────────────

        private void HandleAbilities()
        {
            if (_abilitySystem == null) return;

            // Don't cast spells while in build mode (number keys used for recipe selection)
            var buildCtrl = GetComponent<BuildingController>();
            if (buildCtrl != null && buildCtrl.IsBuildModeActive) return;

            if (_inputHandler.ConsumeCastSpell1())
            {
                Debug.Log("[PlayerMovement] Spell 1 triggered.");
                bool ok = _abilitySystem.TryCastAbility(0, transform.position + transform.forward * 5f);
                if (ok)
                {
                    if (_netAnimator != null) _netAnimator.SetTrigger("Attack");
                }
                else
                {
                    Debug.Log("[PlayerMovement] Spell 1 did not execute (empty slot / cooldown / insufficient mana).");
                }
            }

            if (_inputHandler.ConsumeCastSpell2())
            {
                Debug.Log("[PlayerMovement] Spell 2 triggered.");
                bool ok = _abilitySystem.TryCastAbility(1, transform.position + transform.forward * 5f);
                if (ok)
                {
                    if (_netAnimator != null) _netAnimator.SetTrigger("Attack");
                }
                else
                {
                    Debug.Log("[PlayerMovement] Spell 2 did not execute (empty slot / cooldown / insufficient mana).");
                }
            }
        }
    }
}
