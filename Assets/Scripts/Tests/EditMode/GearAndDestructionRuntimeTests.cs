using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ByteWar.Survival;

namespace ByteWar.Tests.EditMode
{
    public class GearAndDestructionRuntimeTests
    {
        [Test]
        public void EquippedLoadoutState_SetAndWithItemId_TracksPerSlotIds()
        {
            var state = new EquippedLoadoutState();
            state.SetItemId(GearSlot.Helmet, "helm_01");
            state.SetItemId(GearSlot.Chest, "chest_02");

            var updated = state.WithItemId(GearSlot.MainHand, "weapon_03");

            Assert.AreEqual("helm_01", state.GetItemId(GearSlot.Helmet));
            Assert.AreEqual("chest_02", state.GetItemId(GearSlot.Chest));
            Assert.AreEqual(string.Empty, state.GetItemId(GearSlot.MainHand));
            Assert.AreEqual("weapon_03", updated.GetItemId(GearSlot.MainHand));
            Debug.Log("[GearAndDestructionRuntimeTests] Loadout state slot mapping validated.");
        }

        [Test]
        public void GearVisualProfileRegistry_TryResolve_UsesItemIdLookup()
        {
            var registry = ScriptableObject.CreateInstance<GearVisualProfileRegistry>();
            var helmProfile = ScriptableObject.CreateInstance<GearVisualProfile>();
            var chestProfile = ScriptableObject.CreateInstance<GearVisualProfile>();

            try
            {
                helmProfile.ItemId = "helm_01";
                helmProfile.Slot = GearSlot.Helmet;

                chestProfile.ItemId = "chest_02";
                chestProfile.Slot = GearSlot.Chest;

                registry.SetProfilesForTests(new List<GearVisualProfile> { helmProfile, chestProfile });

                Assert.IsTrue(registry.TryResolve("helm_01", out var resolvedHelm));
                Assert.AreEqual(helmProfile, resolvedHelm);

                Assert.IsTrue(registry.TryResolve("chest_02", out var resolvedChest));
                Assert.AreEqual(chestProfile, resolvedChest);

                Assert.IsFalse(registry.TryResolve("unknown", out _));
                Debug.Log("[GearAndDestructionRuntimeTests] Gear profile registry lookup validated.");
            }
            finally
            {
                Object.DestroyImmediate(registry);
                Object.DestroyImmediate(helmProfile);
                Object.DestroyImmediate(chestProfile);
            }
        }

        [Test]
        public void DamageResolver_ResolveEffectiveDamage_AppliesProfileMultiplier()
        {
            var profile = ScriptableObject.CreateInstance<DestructibleProfile>();

            try
            {
                profile.DamageMultipliersMutable.Add(new DamageTypeMultiplier
                {
                    DamageType = DamageType.Axe,
                    Multiplier = 1.5f,
                });

                float axeDamage = DamageResolver.ResolveEffectiveDamage(20f, DamageType.Axe, profile);
                float bluntDamage = DamageResolver.ResolveEffectiveDamage(20f, DamageType.Blunt, profile);

                Assert.AreEqual(30f, axeDamage, 0.0001f);
                Assert.AreEqual(20f, bluntDamage, 0.0001f);
                Debug.Log("[GearAndDestructionRuntimeTests] Damage resolver multiplier application validated.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void DestructibleStateEvaluator_ResolveState_UsesThresholdsAndZeroHealthRule()
        {
            var thresholds = new List<DestructibleStateThreshold>
            {
                new DestructibleStateThreshold { State = DestructibleStateType.Intact, NormalizedHealthThreshold = 1f },
                new DestructibleStateThreshold { State = DestructibleStateType.Damaged, NormalizedHealthThreshold = 0.65f },
            };

            Assert.AreEqual(DestructibleStateType.Intact, DestructibleStateEvaluator.ResolveState(90f, 100f, thresholds));
            Assert.AreEqual(DestructibleStateType.Damaged, DestructibleStateEvaluator.ResolveState(60f, 100f, thresholds));
            Assert.AreEqual(DestructibleStateType.Destroyed, DestructibleStateEvaluator.ResolveState(0f, 100f, thresholds));

            var runtime = DestructibleStateEvaluator.BuildRuntimeState(60f, 100f, DamageType.Axe, thresholds);
            Assert.AreEqual(DestructibleStateType.Damaged, runtime.State);
            Assert.AreEqual(60f, runtime.CurrentHealth, 0.0001f);
            Assert.AreEqual(DamageType.Axe, runtime.LastDamageType);
            Debug.Log("[GearAndDestructionRuntimeTests] Destructible state evaluation validated.");
        }
    }
}