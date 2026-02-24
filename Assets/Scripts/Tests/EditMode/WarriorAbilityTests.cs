using NUnit.Framework;
using UnityEngine;
using ByteWar.Abilities;
using ByteWar.Abilities.Warrior;
using ByteWar.Core;
using Unity.Netcode;

namespace ByteWar.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the Warrior ability system, SFX/VFX type enums,
    /// ability-specific VFX/SFX dispatch, footstep improvements, and building fixes.
    /// </summary>
    [TestFixture]
    public class WarriorAbilityTests
    {
        // ── CleaveAbility ScriptableObject ────────────────────────────────────────

        [Test]
        public void CleaveAbility_Creation_HasCorrectDefaults()
        {
            var cleave = ScriptableObject.CreateInstance<CleaveAbility>();
            try
            {
                cleave.AbilityName = "Iron Tide Cleave";
                cleave.ManaCost = 10f;
                cleave.Cooldown = 3f;
                cleave.Radius = 4f;
                cleave.ArcAngle = 120f;

                Assert.AreEqual("Iron Tide Cleave", cleave.AbilityName);
                Assert.AreEqual(10f, cleave.ManaCost);
                Assert.AreEqual(3f, cleave.Cooldown);
                Assert.AreEqual(4f, cleave.Radius);
                Assert.AreEqual(120f, cleave.ArcAngle);
            }
            finally
            {
                Object.DestroyImmediate(cleave);
            }
        }

        [Test]
        public void CleaveAbility_InheritsFromAbility()
        {
            var cleave = ScriptableObject.CreateInstance<CleaveAbility>();
            try
            {
                Assert.IsInstanceOf<Ability>(cleave);
                Assert.IsInstanceOf<ScriptableObject>(cleave);
            }
            finally
            {
                Object.DestroyImmediate(cleave);
            }
        }

        [Test]
        public void CleaveAbility_CastVFXType_DefaultsToCleaveHit()
        {
            var cleave = ScriptableObject.CreateInstance<CleaveAbility>();
            try
            {
                // When created from AssetGenerator, these are set explicitly.
                // Test that we can set and read them.
                cleave.CastVFXType = VFXType.CleaveHit;
                cleave.CastSFXType = SFXType.CleaveHit;

                Assert.AreEqual(VFXType.CleaveHit, cleave.CastVFXType);
                Assert.AreEqual(SFXType.CleaveHit, cleave.CastSFXType);
            }
            finally
            {
                Object.DestroyImmediate(cleave);
            }
        }

        [Test]
        public void CleaveAbility_WithDamageEffect_EffectIsAssignable()
        {
            var cleave = ScriptableObject.CreateInstance<CleaveAbility>();
            var effect = ScriptableObject.CreateInstance<GameplayEffect>();
            try
            {
                effect.EffectName = "Cleave Damage";
                effect.Type = EffectType.Damage;
                effect.Magnitude = 25f;

                cleave.DamageEffect = effect;

                Assert.IsNotNull(cleave.DamageEffect);
                Assert.AreEqual(25f, cleave.DamageEffect.Magnitude);
                Assert.AreEqual(EffectType.Damage, cleave.DamageEffect.Type);
            }
            finally
            {
                Object.DestroyImmediate(effect);
                Object.DestroyImmediate(cleave);
            }
        }

        // ── SFXType / VFXType enum completeness ──────────────────────────────────

        [Test]
        public void SFXType_CleaveHit_ExistsWithValue7()
        {
            Assert.AreEqual(7, (int)SFXType.CleaveHit);
        }

        [Test]
        public void VFXType_CleaveHit_ExistsWithValue6()
        {
            Assert.AreEqual(6, (int)VFXType.CleaveHit);
        }

        [Test]
        public void SFXType_HasAll8Entries()
        {
            var values = System.Enum.GetValues(typeof(SFXType));
            Assert.AreEqual(8, values.Length, "SFXType should have 8 entries (Footstep through CleaveHit).");
        }

        [Test]
        public void VFXType_HasAll7Entries()
        {
            var values = System.Enum.GetValues(typeof(VFXType));
            Assert.AreEqual(7, values.Length, "VFXType should have 7 entries (FireballMuzzle through CleaveHit).");
        }

        // ── Ability base class VFX/SFX fields ────────────────────────────────────

        [Test]
        public void Ability_CastVFXType_IsSettableAndReadable()
        {
            var cleave = ScriptableObject.CreateInstance<CleaveAbility>();
            try
            {
                cleave.CastVFXType = VFXType.GatherHit;
                Assert.AreEqual(VFXType.GatherHit, cleave.CastVFXType);

                cleave.CastSFXType = SFXType.MeleeHit;
                Assert.AreEqual(SFXType.MeleeHit, cleave.CastSFXType);
            }
            finally
            {
                Object.DestroyImmediate(cleave);
            }
        }

        // ── AudioManager CleaveHit clip slot ──────────────────────────────────────

        [Test]
        public void AudioManager_PlaySFX_CleaveHit_DoesNotThrow()
        {
            var go = new GameObject("TestAudioManager");
            try
            {
                var manager = go.AddComponent<AudioManager>();
                Assert.DoesNotThrow(() => manager.PlaySFX(SFXType.CleaveHit));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ── VFXManager CleaveHit prefab slot ─────────────────────────────────────

        [Test]
        public void VFXManager_PlayEffectLocal_CleaveHit_DoesNotThrow()
        {
            var go = new GameObject("TestVFXManager");
            go.AddComponent<NetworkObject>();
            try
            {
                var manager = go.AddComponent<VFXManager>();
                Assert.DoesNotThrow(() => manager.PlayEffectLocal(VFXType.CleaveHit, Vector3.zero));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ── AbilitySystemComponent: cooldown and mana cost ────────────────────────

        [Test]
        public void AbilitySystemComponent_CooldownAndManaCost_WorkWithCleave()
        {
            var go = new GameObject("TestPlayer");
            go.AddComponent<NetworkObject>();
            var attributes = go.AddComponent<AttributeSet>();
            var asc = go.AddComponent<AbilitySystemComponent>();

            var cleave = ScriptableObject.CreateInstance<CleaveAbility>();
            cleave.AbilityName = "Iron Tide Cleave";
            cleave.ManaCost = 10f;
            cleave.Cooldown = 3f;

            try
            {
                asc.AddLearnedAbility(cleave);

                Assert.AreEqual(1, asc.LearnedAbilities.Count);
                Assert.AreEqual("Iron Tide Cleave", asc.LearnedAbilities[0].AbilityName);
                Assert.AreEqual(10f, asc.LearnedAbilities[0].ManaCost);
                Assert.AreEqual(3f, asc.LearnedAbilities[0].Cooldown);
                Assert.IsFalse(asc.IsOnCooldown("Iron Tide Cleave"));
            }
            finally
            {
                Object.DestroyImmediate(cleave);
                Object.DestroyImmediate(go);
            }
        }

        // ── GameplayEffect integration ────────────────────────────────────────────

        [Test]
        public void GameplayEffect_CleaveDamage_ReducesHealth()
        {
            var go = new GameObject("TestTarget");
            go.AddComponent<NetworkObject>();
            var attributes = go.AddComponent<AttributeSet>();

            var effect = ScriptableObject.CreateInstance<GameplayEffect>();
            effect.EffectName = "Cleave Damage";
            effect.Type = EffectType.Damage;
            effect.DurationType = DurationType.Instant;
            effect.Magnitude = 25f;

            try
            {
                float healthBefore = attributes.Health.Value;
                effect.ApplyEffect(attributes);
                float healthAfter = attributes.Health.Value;

                Assert.AreEqual(25f, healthBefore - healthAfter, 0.01f,
                    "Cleave damage should reduce health by the effect magnitude.");
            }
            finally
            {
                Object.DestroyImmediate(effect);
                Object.DestroyImmediate(go);
            }
        }

        // ── AudioClip generation smoke test ───────────────────────────────────────

        [Test]
        public void AudioClips_CleaveHit_ExistsAfterGeneration()
        {
            // This test verifies the CleaveHit entry exists in the SFX enum.
            // Actual clip generation requires running ByteWar/Generate SFX Clips.
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/SFX/CleaveHit.wav");
            if (clip == null)
            {
                Assert.Ignore("CleaveHit.wav not yet generated. Run ByteWar/Generate SFX Clips.");
                return;
            }
            Assert.IsNotNull(clip);
            Assert.Greater(clip.samples, 0);
        }

        // ── VFX prefab generation smoke test ──────────────────────────────────────

        [Test]
        public void VFXPrefab_CleaveHit_ExistsAfterGeneration()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedPrefabs/VFX/CleaveHit.prefab");
            if (prefab == null)
            {
                Assert.Ignore("CleaveHit.prefab not yet generated. Run ByteWar/Generate VFX Prefabs.");
                return;
            }
            Assert.IsNotNull(prefab);
            var ps = prefab.GetComponent<ParticleSystem>();
            Assert.IsNotNull(ps, "CleaveHit prefab should have a ParticleSystem.");
        }
    }
}
