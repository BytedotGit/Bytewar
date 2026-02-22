using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using SurvivalRPG.Abilities;
using SurvivalRPG.Survival;
using SurvivalRPG.Core;

namespace SurvivalRPG.Networking
{
    [RequireComponent(typeof(AbilitySystemComponent))]
    [RequireComponent(typeof(SurvivalStats))]
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(CharacterController))]
    public class NetworkPlayer : NetworkBehaviour
    {
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
        }

        public override void OnNetworkSpawn()
        {
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
            _initialized = true;
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
            _camController.SetTarget(pivot != null ? pivot : transform);
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
                if (_netAnimator != null) _netAnimator.SetTrigger("Attack");
                AbilitySystem.TryCastAbility(0, transform.position + transform.forward * 5f);
            }

            if (_inputHandler.ConsumeCastSpell2())
            {
                Debug.Log("[NetworkPlayer] Spell 2 triggered.");
                if (_netAnimator != null) _netAnimator.SetTrigger("Attack");
                AbilitySystem.TryCastAbility(1, transform.position + transform.forward * 5f);
            }
        }
    }
}
