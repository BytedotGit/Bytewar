using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;
using System.IO;

namespace SurvivalRPG.Editor
{
    public static class AnimatorGenerator
    {
        private const string PlaceholderFolder = "Assets/GeneratedPrefabs/Animations/Placeholders";

        [MenuItem("SurvivalRPG/Generate Animator Controller")]
        public static void GenerateAnimatorController()
        {
            Debug.Log("[AnimatorGenerator] Generating Animator Controller...");

            string basePath = "Assets/GeneratedPrefabs/Animations";
            if (!AssetDatabase.IsValidFolder(basePath))
            {
                if (!AssetDatabase.IsValidFolder("Assets/GeneratedPrefabs"))
                {
                    AssetDatabase.CreateFolder("Assets", "GeneratedPrefabs");
                }
                AssetDatabase.CreateFolder("Assets/GeneratedPrefabs", "Animations");
            }

            string controllerPath = $"{basePath}/PlayerAnimatorController.controller";
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // Add parameters
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

            // Get the base layer
            AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

            // Try to find Mixamo clips
            bool mixamoEnabled = MixamoLocalAssets.AreAvailable(out string reason);
            if (!mixamoEnabled)
            {
                Debug.Log($"[AnimatorGenerator] Mixamo not available locally ({reason}). Generating placeholder clips.");
                EnsureFolder(PlaceholderFolder);
            }

            AnimationClip idleClip = mixamoEnabled ? FindClip("Idle") : GetOrCreatePlaceholderClip("Idle", loop: true);
            AnimationClip walkClip = mixamoEnabled ? FindClip("Walking") : GetOrCreatePlaceholderClip("Walking", loop: true);
            AnimationClip runClip = mixamoEnabled ? FindClip("Running") : GetOrCreatePlaceholderClip("Running", loop: true);
            AnimationClip jumpClip = mixamoEnabled ? FindClip("Jump") : GetOrCreatePlaceholderClip("Jump", loop: false);
            AnimationClip attackClip = mixamoEnabled ? FindClip("Attack") : GetOrCreatePlaceholderClip("Attack", loop: false);

            // Create states
            AnimatorState idleState = rootStateMachine.AddState("Idle");
            idleState.motion = idleClip;

            // Create Blend Tree for Movement
            BlendTree moveBlendTree;
            AnimatorState moveState = controller.CreateBlendTreeInController("Move", out moveBlendTree);
            moveBlendTree.blendType = BlendTreeType.Simple1D;
            moveBlendTree.blendParameter = "Speed";

            if (idleClip != null) moveBlendTree.AddChild(idleClip, 0f);
            if (walkClip != null) moveBlendTree.AddChild(walkClip, 3f); // Walk speed
            if (runClip != null) moveBlendTree.AddChild(runClip, 6f);   // Run speed

            AnimatorState jumpState = rootStateMachine.AddState("Jump");
            jumpState.motion = jumpClip;

            AnimatorState attackState = rootStateMachine.AddState("Attack");
            attackState.motion = attackClip;

            // Set default state
            rootStateMachine.defaultState = idleState;

            // Create transitions
            // Idle -> Move
            AnimatorStateTransition idleToMove = idleState.AddTransition(moveState);
            idleToMove.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            idleToMove.duration = 0.1f;

            // Move -> Idle
            AnimatorStateTransition moveToIdle = moveState.AddTransition(idleState);
            moveToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            moveToIdle.duration = 0.1f;

            // Any -> Jump
            AnimatorStateTransition anyToJump = rootStateMachine.AddAnyStateTransition(jumpState);
            anyToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            anyToJump.duration = 0.1f;

            // Jump -> Idle
            AnimatorStateTransition jumpToIdle = jumpState.AddTransition(idleState);
            jumpToIdle.hasExitTime = true;
            jumpToIdle.exitTime = 0.8f;
            jumpToIdle.duration = 0.2f;

            // Any -> Attack
            AnimatorStateTransition anyToAttack = rootStateMachine.AddAnyStateTransition(attackState);
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            anyToAttack.duration = 0.1f;

            // Attack -> Idle
            AnimatorStateTransition attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.8f;
            attackToIdle.duration = 0.2f;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AnimatorGenerator] Created Animator Controller at {controllerPath}");
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static AnimationClip GetOrCreatePlaceholderClip(string name, bool loop)
        {
            string path = $"{PlaceholderFolder}/{name}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null) return existing;

            var clip = new AnimationClip
            {
                name = name,
                legacy = false
            };

            // Placeholder clips are intentionally empty; they provide a valid reference
            // so the AnimatorController has motions even in public repo clones.
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, path);
            Debug.Log($"[AnimatorGenerator] Created placeholder clip: {path}");
            return clip;
        }

        private static AnimationClip FindClip(string name)
        {
            const string mixamoFolder = "Assets/Art/Characters/Mixamo";
            // Search for the FBX file by name (reliable; FindAssets t:Model works on the file)
            string[] guids = AssetDatabase.FindAssets($"t:Model {name}", new[] { mixamoFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!string.Equals(fileName, name, System.StringComparison.OrdinalIgnoreCase)) continue;

                // Load all sub-assets; the AnimationClip is embedded in the FBX
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (Object asset in assets)
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        Debug.Log($"[AnimatorGenerator] Found clip '{clip.name}' in {path}");
                        return clip;
                    }
                }
            }
            Debug.LogWarning($"[AnimatorGenerator] Clip '{name}' not found in {mixamoFolder}");
            return null;
        }
    }
}