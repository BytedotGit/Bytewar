using System.Collections;
using UnityEngine;
using Unity.Netcode;
using ByteWar.Core;

namespace ByteWar.Networking
{
    /// <summary>
    /// Handles visual grounding, animator controller repair, and visual mode detection.
    /// Extracted from NetworkPlayer to keep single-responsibility.
    /// Must be on the same GameObject as NetworkPlayer.
    /// </summary>
    public class PlayerVisualSetup : NetworkBehaviour
    {
        private const string ResourcesAnimatorControllerPath = "Generated/PlayerAnimatorController";

        private Animator _animator;
        private CharacterController _cc;

        private bool _runtimeVisualGroundingStarted;
        private bool _runtimeVisualGroundingApplied;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _cc = GetComponent<CharacterController>();

            EnsureAnimatorControllerAssigned("Awake");
        }

        public override void OnNetworkSpawn()
        {
            EnsureAnimatorControllerAssigned("OnNetworkSpawn");

            if (!_runtimeVisualGroundingStarted)
            {
                _runtimeVisualGroundingStarted = true;
                StartCoroutine(RuntimeApplyVisualGroundingOnce());
            }
        }

        // ── Animator Controller Repair ────────────────────────────────────────────

        internal void EnsureAnimatorControllerAssigned(string context)
        {
            if (_animator == null) return;
            if (_animator.runtimeAnimatorController != null) return;

            var controller = Resources.Load<RuntimeAnimatorController>(ResourcesAnimatorControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[PlayerVisualSetup] AnimatorController missing at runtime (context={context}). Resources.Load failed for '{ResourcesAnimatorControllerPath}'.");
                return;
            }

            _animator.runtimeAnimatorController = controller;
            _animator.Rebind();
            _animator.Update(0f);
            Debug.LogWarning($"[PlayerVisualSetup] Repaired null AnimatorController via Resources (context={context}) -> '{controller.name}'.");
        }

        // ── Visual Grounding ──────────────────────────────────────────────────────

        private IEnumerator RuntimeApplyVisualGroundingOnce()
        {
            const float maxWaitSeconds = 2.0f;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < maxWaitSeconds)
            {
                if (_cc != null && _cc.enabled && _cc.isGrounded)
                    break;
                yield return null;
            }

            yield return null;

            if (_runtimeVisualGroundingApplied)
                yield break;

            const int maxAttempts = 4;
            float totalApplied = 0f;
            string lastDiag = "";
            bool anyApplied = false;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                bool applied = TryApplyVisualGroundingNow(desiredDelta: -0.035f, maxOffset: 0.60f, out float appliedOffset, out string diagnostics);
                lastDiag = diagnostics;
                if (applied)
                {
                    anyApplied = true;
                    totalApplied += appliedOffset;
                }
                else
                {
                    break;
                }
                yield return null;
            }

            if (IsOwner)
            {
                if (anyApplied)
                {
                    Debug.Log($"[PlayerVisualSetup] Visual grounding applied: totalOffset={totalApplied:0.000} diag={lastDiag}");

                    // Refresh camera pivot after visual root moved so third-person framing stays consistent.
                    var netPlayer = GetComponent<NetworkPlayer>();
                    if (netPlayer != null) netPlayer.SetupCamera();
                }
                else
                {
                    Debug.Log($"[PlayerVisualSetup] Visual grounding not needed: diag={lastDiag}");
                }
            }

            _runtimeVisualGroundingApplied = anyApplied;
        }

        internal bool TryApplyVisualGroundingNow(float desiredDelta, float maxOffset, out float appliedOffset, out string diagnostics)
        {
            appliedOffset = 0f;
            diagnostics = "";

            Transform visualRoot = transform.Find("VisualRoot");
            if (visualRoot == null)
                return false;

            if (!VisualGroundingUtility.TryGetSupportGroundY(transform.position, ignoreRoot: transform, _animator, out float groundY, out string groundSource))
            {
                if (IsOwner)
                    Debug.LogWarning("[PlayerVisualSetup] Visual grounding skipped: unable to determine groundY.");
                return false;
            }

            if (!VisualGroundingUtility.TrySampleVisualBottomY(visualRoot, _animator, out var sample))
            {
                if (IsOwner)
                    Debug.LogWarning("[PlayerVisualSetup] Visual grounding skipped: unable to sample visual bottom Y.");
                return false;
            }

            bool applied = VisualGroundingUtility.TryApplyHoverCorrection(
                visualRoot,
                visualBottomY: sample.SelectedBottomY,
                groundY: groundY,
                desiredDelta: desiredDelta,
                maxOffset: maxOffset,
                out appliedOffset);

            diagnostics = $"groundY={groundY:0.000} source={groundSource} visualY={sample.SelectedBottomY:0.000} method={sample.Method} details=({sample.Details})";
            return applied;
        }

        // ── Visual Mode Detection ─────────────────────────────────────────────────

        public PlayerVisualMode DetectActualVisualMode(out NetworkPlayer.VisualDiagnostics diagnostics)
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

            diagnostics = new NetworkPlayer.VisualDiagnostics
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
    }
}
