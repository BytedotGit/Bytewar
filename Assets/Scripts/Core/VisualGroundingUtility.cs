using System;
using UnityEngine;

namespace ByteWar.Core
{
    internal static class VisualGroundingUtility
    {
        internal readonly struct VisualBottomSample
        {
            public readonly bool HasHumanoidBones;
            public readonly float HumanoidBottomY;
            public readonly bool HasRenderers;
            public readonly float RendererBottomY;
            public readonly float SelectedBottomY;
            public readonly string Method;
            public readonly string Details;

            public VisualBottomSample(
                bool hasHumanoidBones,
                float humanoidBottomY,
                bool hasRenderers,
                float rendererBottomY,
                float selectedBottomY,
                string method,
                string details)
            {
                HasHumanoidBones = hasHumanoidBones;
                HumanoidBottomY = humanoidBottomY;
                HasRenderers = hasRenderers;
                RendererBottomY = rendererBottomY;
                SelectedBottomY = selectedBottomY;
                Method = method;
                Details = details;
            }
        }

        internal static bool TryGetGroundY(Vector3 worldPos, out float groundY, out string source)
        {
            return TryGetGroundY(worldPos, ignoreRoot: null, out groundY, out source);
        }

        internal static bool TryGetGroundY(Vector3 worldPos, Transform ignoreRoot, out float groundY, out string source)
        {
            // Prefer an actual physics hit first so we align to rocks/props above terrain.
            // Ignore hits on the player's own hierarchy.
            Vector3 rayOrigin = worldPos + Vector3.up * 2f;
            const float maxDistance = 10f;

            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            int hitCount = hits != null ? hits.Length : 0;

            float bestDistance = float.PositiveInfinity;
            int bestIndex = -1;
            for (int i = 0; i < hitCount; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;

                Transform ht = h.collider.transform;
                if (ignoreRoot != null && ht != null && ht.IsChildOf(ignoreRoot))
                    continue;

                if (h.distance < bestDistance)
                {
                    bestDistance = h.distance;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                groundY = hits[bestIndex].point.y;
                source = $"Physics.RaycastAll(hit='{hits[bestIndex].collider.name}')";
                return true;
            }

            // Fallback: Terrain height.
            if (Terrain.activeTerrain != null)
            {
                var t = Terrain.activeTerrain;
                groundY = t.SampleHeight(worldPos) + t.transform.position.y;
                source = "Terrain.SampleHeight";
                return true;
            }

            groundY = 0f;
            source = "(none)";
            return false;
        }

        internal static bool TryGetSupportGroundY(Vector3 fallbackWorldPos, Transform ignoreRoot, Animator animator, out float groundY, out string source)
        {
            // Prefer support point(s) under the feet when humanoid. On uneven terrain, the root XZ can be over a dip,
            // which makes the character appear to hover even if the capsule is grounded.
            if (animator != null && animator.isHuman)
            {
                Transform lt = animator.GetBoneTransform(HumanBodyBones.LeftToes) ?? animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rt = animator.GetBoneTransform(HumanBodyBones.RightToes) ?? animator.GetBoneTransform(HumanBodyBones.RightFoot);

                bool hasAny = false;
                float best = float.NegativeInfinity;
                string bestSrc = "";

                if (lt != null && TryRaycastDownAt(lt.position, ignoreRoot, out float yL, out string srcL))
                {
                    hasAny = true;
                    if (yL > best)
                    {
                        best = yL;
                        bestSrc = $"Support(L:{srcL})";
                    }
                }

                if (rt != null && TryRaycastDownAt(rt.position, ignoreRoot, out float yR, out string srcR))
                {
                    hasAny = true;
                    if (yR > best)
                    {
                        best = yR;
                        bestSrc = $"Support(R:{srcR})";
                    }
                }

                if (hasAny)
                {
                    groundY = best;
                    source = bestSrc;
                    return true;
                }
            }

            // Fallback: root-based sample.
            return TryGetGroundY(fallbackWorldPos, ignoreRoot, out groundY, out source);
        }

        internal static bool TrySampleVisualBottomY(Transform visualRoot, Animator animator, out VisualBottomSample sample)
        {
            if (visualRoot == null)
            {
                sample = default;
                return false;
            }

            bool hasHumanoid = TrySampleHumanoidBottomY(animator, out float humanoidBottomY, out string humanoidDetails);
            bool hasRenderers = TrySampleRendererBottomY(visualRoot, out float rendererBottomY, out int rendererCount);

            if (!hasHumanoid && !hasRenderers)
            {
                sample = new VisualBottomSample(
                    hasHumanoidBones: false,
                    humanoidBottomY: float.PositiveInfinity,
                    hasRenderers: false,
                    rendererBottomY: float.PositiveInfinity,
                    selectedBottomY: float.PositiveInfinity,
                    method: "None",
                    details: "no humanoid bones and no renderers");
                return false;
            }

            float selected;
            string method;

            if (hasHumanoid)
            {
                // Prefer humanoid bones (toe/foot bone positions): they are the most reliable proxy
                // for "foot sole height" on skinned meshes. SkinnedMeshRenderer.bounds are unreliable
                // because they include rigging/skinning padding and vary per animation pose.
                // Only fall back to renderer bounds if humanoidBottomY looks wildly wrong (e.g., far above renderers).
                if (hasRenderers && humanoidBottomY > rendererBottomY + 0.35f)
                {
                    selected = rendererBottomY;
                    method = "RendererBounds";
                }
                else
                {
                    selected = humanoidBottomY;
                    method = "HumanoidBones";
                }
            }
            else
            {
                selected = rendererBottomY;
                method = "RendererBounds";
            }

            string details = $"humanoid={hasHumanoid} humanoidY={humanoidBottomY:0.000} ({humanoidDetails}) renderers={hasRenderers} rendererY={rendererBottomY:0.000} rendererCount={rendererCount}";
            sample = new VisualBottomSample(
                hasHumanoidBones: hasHumanoid,
                humanoidBottomY: humanoidBottomY,
                hasRenderers: hasRenderers,
                rendererBottomY: rendererBottomY,
                selectedBottomY: selected,
                method: method,
                details: details);
            return true;
        }

        internal static bool TryApplyHoverCorrection(
            Transform visualRoot,
            float visualBottomY,
            float groundY,
            float desiredDelta,
            float maxOffset,
            out float appliedOffset)
        {
            appliedOffset = 0f;
            if (visualRoot == null) return false;

            float delta = visualBottomY - groundY;
            // If delta is positive, feet are above the ground. A small negative desiredDelta intentionally sinks
            // the visual a tiny amount to avoid perceptible "air gaps" due to bounds/lighting.
            if (delta <= desiredDelta)
            {
                return false; // not hovering (or already slightly below)
            }

            float needed = delta - desiredDelta;
            float offset = Mathf.Min(needed, maxOffset);
            if (offset <= 0f) return false;

            // Move in world-space so parent scale/rotation doesn't affect the correction.
            visualRoot.position -= new Vector3(0f, offset, 0f);
            appliedOffset = offset;
            return true;
        }

        private static bool TrySampleHumanoidBottomY(Animator animator, out float bottomY, out string details)
        {
            bottomY = float.PositiveInfinity;
            details = "(no animator)";

            if (animator == null) return false;
            if (!animator.isHuman) { details = "animator.isHuman=false"; return false; }

            // Prefer toes when available; fall back to feet.
            Transform lt = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            Transform rt = animator.GetBoneTransform(HumanBodyBones.RightToes);
            Transform lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);

            int count = 0;

            if (lt != null) { bottomY = Math.Min(bottomY, lt.position.y); count++; }
            if (rt != null) { bottomY = Math.Min(bottomY, rt.position.y); count++; }

            if (count == 0)
            {
                if (lf != null) { bottomY = Math.Min(bottomY, lf.position.y); count++; }
                if (rf != null) { bottomY = Math.Min(bottomY, rf.position.y); count++; }
            }

            if (count == 0)
            {
                details = "no foot/toe bones";
                return false;
            }

            details = (lt != null || rt != null) ? "toes" : "feet";
            return true;
        }

        private static bool TrySampleRendererBottomY(Transform visualRoot, out float bottomY, out int rendererCount)
        {
            bottomY = float.PositiveInfinity;
            rendererCount = 0;

            var renderers = visualRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null || renderers.Length == 0) return false;

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;

                // Bounds are world-space.
                bottomY = Math.Min(bottomY, r.bounds.min.y);
                rendererCount++;
            }

            return rendererCount > 0 && !float.IsInfinity(bottomY) && !float.IsNaN(bottomY);
        }

        private static bool TryRaycastDownAt(Vector3 worldPos, Transform ignoreRoot, out float hitY, out string source)
        {
            // Short cast from just above the sample point; we only care about nearby support.
            Vector3 rayOrigin = worldPos + Vector3.up * 0.50f;
            const float maxDistance = 3f;

            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            int hitCount = hits != null ? hits.Length : 0;

            float bestDistance = float.PositiveInfinity;
            int bestIndex = -1;
            for (int i = 0; i < hitCount; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;

                Transform ht = h.collider.transform;
                if (ignoreRoot != null && ht != null && ht.IsChildOf(ignoreRoot))
                    continue;

                if (h.distance < bestDistance)
                {
                    bestDistance = h.distance;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                hitY = hits[bestIndex].point.y;
                source = $"Ray(hit='{hits[bestIndex].collider.name}')";
                return true;
            }

            hitY = 0f;
            source = "Ray(miss)";
            return false;
        }
    }
}
