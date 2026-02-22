using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using SurvivalRPG.Editor;
using SurvivalRPG.Networking;

namespace SurvivalRPG.Tests.EditMode
{
    public class PlayerVisualModeStampTests
    {
        private const string NetworkPlayerPrefabPath = "Assets/GeneratedPrefabs/NetworkPlayer.prefab";

        [Test]
        public void NetworkPlayerPrefab_VisualModeStamp_MatchesDetectedHierarchy()
        {
            EnsurePrefabStamped();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPlayerPrefabPath);
            Assert.IsNotNull(prefab, "NetworkPlayer.prefab not found after regeneration.");

            var instance = Object.Instantiate(prefab);
            try
            {
                var np = instance.GetComponent<NetworkPlayer>();
                Assert.IsNotNull(np, "NetworkPlayer component missing from prefab instance.");

                Assert.AreNotEqual(PlayerVisualMode.Unknown, np.GeneratedVisualModeStamp,
                    "Generated visual mode stamp is Unknown; PrefabGenerator must set it.");

                var actual = np.DetectActualVisualMode(out NetworkPlayer.VisualDiagnostics diag);

                Assert.AreEqual(np.GeneratedVisualModeStamp, actual,
                    $"Stamp/actual mismatch. stamp={np.GeneratedVisualModeStamp} actual={actual} " +
                    $"child='{diag.VisualRootChildName}' skinned={diag.SkinnedMeshRendererCount} renderers={diag.RendererCount} mixamoRig={diag.HasMixamoRig}");

                if (actual == PlayerVisualMode.Mixamo)
                {
                    Assert.Greater(diag.SkinnedMeshRendererCount, 0,
                        "Mixamo mode requires at least one SkinnedMeshRenderer.");
                    Assert.IsTrue(diag.HasMixamoRig, "Mixamo mode requires mixamorig:* bones to be present.");
                }

                if (actual == PlayerVisualMode.PrimitiveFallback)
                {
                    Assert.AreEqual("FallbackCapsule", diag.VisualRootChildName,
                        "Primitive fallback should use the deterministic FallbackCapsule child name.");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void EnsurePrefabStamped()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPlayerPrefabPath);
            bool needsRegen = prefab == null;

            if (!needsRegen)
            {
                var instance = Object.Instantiate(prefab);
                try
                {
                    var np = instance.GetComponent<NetworkPlayer>();
                    needsRegen = (np == null) || (np.GeneratedVisualModeStamp == PlayerVisualMode.Unknown);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            if (!needsRegen) return;

            // Keep this test self-healing: a fresh clone should be able to generate fallbacks,
            // while a dev machine with Mixamo FBX will generate/stamp Mixamo visuals.
            AnimatorGenerator.GenerateAnimatorController();
            PrefabGenerator.GeneratePrefabs();
        }
    }
}
