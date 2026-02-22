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

        // ── Movement ──────────────────────────────────────────────────────────────

        private void HandleMovement()
        {
            Vector2 input = _inputHandler.MovementInput;

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
                    _verticalVelocity = GameConstants.GetJumpForce();
                    if (_netAnimator != null) _netAnimator.SetTrigger("Jump");
                }
            }
            else
            {
                _verticalVelocity += GameConstants.GetGravity() * Time.deltaTime;
            }

            Vector3 velocity = moveDir.normalized * GameConstants.GetMoveSpeed();
            velocity.y = _verticalVelocity;
            _cc.Move(velocity * Time.deltaTime);

            // Rotation
            if (isMoving)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                                                        Time.deltaTime * GameConstants.GetTurnSpeed());
            }
            else if (_camController != null && _camController.IsRightMouseHeld)
            {
                // Standing + RMB: face camera yaw (WoW behaviour)
                Quaternion faceCam = Quaternion.Euler(0f, camYaw, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, faceCam,
                                                      Time.deltaTime * GameConstants.GetTurnSpeed());
            }

            if (_animator != null)
            {
                _animator.SetFloat("Speed", isMoving ? moveDir.magnitude * GameConstants.GetMoveSpeed() : 0f);
                _animator.SetBool("IsGrounded", _cc.isGrounded);
            }
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
