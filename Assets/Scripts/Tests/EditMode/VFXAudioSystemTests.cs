using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ByteWar.Core;
using ByteWar.Editor;

namespace ByteWar.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for Phase 15: VFX prefabs, audio clips, and AudioManager smoke.
    /// </summary>
    public class VFXAudioSystemTests
    {
        private const string VFXFolder = "Assets/GeneratedPrefabs/VFX";
        private const string SFXFolder = "Assets/Resources/SFX";

        // ── VFX prefab tests ─────────────────────────────────────────────────────

        [Test]
        public void VFXPrefabs_AllSixExist_InGeneratedPrefabsVFXFolder()
        {
            string[] expectedNames = {
                "Fireball_MuzzleFlash",
                "Fireball_Impact",
                "GatherHit",
                "ResourceDeath",
                "BuildingPlace",
                "ItemPickup",
            };

            foreach (string name in expectedNames)
            {
                string path  = $"{VFXFolder}/{name}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, $"VFX prefab missing at {path}. Run ByteWar/Generate VFX Prefabs.");
            }
        }

        [Test]
        public void VFXPrefab_HasParticleSystem_WithStopActionDestroy()
        {
            string path   = $"{VFXFolder}/Fireball_MuzzleFlash.prefab";
            var prefab    = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                Assert.Ignore("VFX prefabs not yet generated. Run ByteWar/Generate VFX Prefabs first.");
                return;
            }

            var instance = Object.Instantiate(prefab);
            try
            {
                var ps = instance.GetComponent<ParticleSystem>();
                Assert.IsNotNull(ps, "VFX prefab is missing a ParticleSystem component.");
                Assert.AreEqual(
                    ParticleSystemStopAction.Destroy, ps.main.stopAction,
                    "VFX prefab stopAction should be Destroy so it auto-destructs.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void VFXPrefab_Loop_IsDisabled()
        {
            // All our VFX should be one-shot bursts, not looping
            string[] names = { "Fireball_MuzzleFlash", "Fireball_Impact", "GatherHit",
                               "ResourceDeath", "BuildingPlace", "ItemPickup" };

            foreach (string name in names)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{VFXFolder}/{name}.prefab");
                if (prefab == null) continue;   // skip if not yet generated

                var instance = Object.Instantiate(prefab);
                try
                {
                    var ps = instance.GetComponent<ParticleSystem>();
                    Assert.IsFalse(ps.main.loop, $"{name}: ParticleSystem.loop should be false for VFX bursts.");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        // ── Audio clip tests ─────────────────────────────────────────────────────

        [Test]
        public void AudioClips_AllSevenExist_InResourcesSFXFolder()
        {
            string[] expectedNames = {
                "Footstep",
                "MeleeHit",
                "FireballCast",
                "FireballImpact",
                "ItemPickup",
                "BuildingPlace",
                "UIClick",
            };

            foreach (string name in expectedNames)
            {
                string path = $"{SFXFolder}/{name}.wav";
                var clip    = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.IsNotNull(clip, $"SFX clip missing at {path}. Run ByteWar/Generate SFX Clips.");
            }
        }

        [Test]
        public void AudioClips_AllLoadableViaResourcesLoad()
        {
            string[] names = { "SFX/Footstep", "SFX/MeleeHit", "SFX/FireballCast",
                               "SFX/FireballImpact", "SFX/ItemPickup", "SFX/BuildingPlace", "SFX/UIClick" };

            foreach (string name in names)
            {
                var clip = Resources.Load<AudioClip>(name);
                Assert.IsNotNull(clip, $"Resources.Load<AudioClip>(\"{name}\") returned null. Check SFX folder is inside Resources/.");
            }
        }

        // ── AudioManager smoke test ───────────────────────────────────────────────

        [Test]
        public void AudioManager_PlaySFX_DoesNotThrowWithNullClip()
        {
            // Create a minimal AudioManager and call PlaySFX without any clips assigned.
            // Should degrade gracefully with a warning, not throw.
            var go = new GameObject("TestAudioManager");
            AudioManager manager = null;

            try
            {
                manager = go.AddComponent<AudioManager>();

                // PlaySFX with a missing clip should log warning but not throw
                Assert.DoesNotThrow(() => manager.PlaySFX(SFXType.Footstep));
                Assert.DoesNotThrow(() => manager.PlaySFX(SFXType.FireballImpact, new Vector3(1, 0, 1)));
            }
            finally
            {
                Object.DestroyImmediate(go);
                // Clear singleton if set
                if (AudioManager.Instance == manager)
                {
                    // Destroying the GO triggers OnDestroy which clears the singleton
                }
            }
        }

        // ── VFXManager smoke test ─────────────────────────────────────────────────

        [Test]
        public void VFXManager_PlayEffectLocal_DoesNotThrowWithNullPrefab()
        {
            var go      = new GameObject("TestVFXManager");
            var netObj  = go.AddComponent<Unity.Netcode.NetworkObject>();
            VFXManager manager = null;

            try
            {
                manager = go.AddComponent<VFXManager>();

                // All prefabs are null on a fresh component — should warn gracefully
                Assert.DoesNotThrow(() => manager.PlayEffectLocal(VFXType.FireballImpact, Vector3.zero));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ── FootstepController unit tests ─────────────────────────────────────────

        [Test]
        public void FootstepController_RequiresCharacterController()
        {
            var go = new GameObject("TestFootstep");
            go.AddComponent<CharacterController>();
            try
            {
                var fc = go.AddComponent<FootstepController>();
                Assert.IsNotNull(fc);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
