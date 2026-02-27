using NUnit.Framework;
using UnityEngine;
using ByteWar.Survival;

namespace ByteWar.Tests.EditMode
{
    public class GearAndDestructionProfileTests
    {
        private GearVisualProfile _gearProfile;
        private DestructibleProfile _destructibleProfile;
        private SupportProfile _supportProfile;

        [SetUp]
        public void SetUp()
        {
            _gearProfile = ScriptableObject.CreateInstance<GearVisualProfile>();
            _destructibleProfile = ScriptableObject.CreateInstance<DestructibleProfile>();
            _supportProfile = ScriptableObject.CreateInstance<SupportProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gearProfile);
            Object.DestroyImmediate(_destructibleProfile);
            Object.DestroyImmediate(_supportProfile);
        }

        [Test]
        public void GearVisualProfile_InternalSetters_UpdateExpectedFields()
        {
            _gearProfile.ItemId = "starter_chest_001";
            _gearProfile.Slot = GearSlot.Chest;
            _gearProfile.VisualType = GearVisualType.SkinnedReplacement;
            _gearProfile.SocketName = "SpineSocket";
            _gearProfile.BodyMask = BodyMaskFlags.Torso | BodyMaskFlags.Arms;

            Assert.AreEqual("starter_chest_001", _gearProfile.ItemId);
            Assert.AreEqual(GearSlot.Chest, _gearProfile.Slot);
            Assert.AreEqual(GearVisualType.SkinnedReplacement, _gearProfile.VisualType);
            Assert.AreEqual("SpineSocket", _gearProfile.SocketName);
            Assert.AreEqual(BodyMaskFlags.Torso | BodyMaskFlags.Arms, _gearProfile.BodyMask);
            Debug.Log("[GearAndDestructionProfileTests] Gear profile setters validated.");
        }

        [Test]
        public void DestructibleProfile_GetDamageMultiplier_ReturnsConfiguredValue()
        {
            _destructibleProfile.DamageMultipliersMutable.Add(new DamageTypeMultiplier
            {
                DamageType = DamageType.Axe,
                Multiplier = 1.5f,
            });

            float multiplier = _destructibleProfile.GetDamageMultiplier(DamageType.Axe);

            Assert.AreEqual(1.5f, multiplier, 0.0001f);
            Debug.Log("[GearAndDestructionProfileTests] Configured damage multiplier validated.");
        }

        [Test]
        public void DestructibleProfile_GetDamageMultiplier_ReturnsDefaultOne_WhenMissing()
        {
            float multiplier = _destructibleProfile.GetDamageMultiplier(DamageType.Fire);

            Assert.AreEqual(1f, multiplier, 0.0001f);
            Debug.Log("[GearAndDestructionProfileTests] Default damage multiplier fallback validated.");
        }

        [Test]
        public void SupportProfile_InternalSetters_UpdateExpectedFields()
        {
            _supportProfile.ProfileId = "wood_tier_1";
            _supportProfile.MaxSupportDistance = 9.5f;
            _supportProfile.CollapseDelaySeconds = 0.4f;
            _supportProfile.AllowDiagonalSupport = false;

            Assert.AreEqual("wood_tier_1", _supportProfile.ProfileId);
            Assert.AreEqual(9.5f, _supportProfile.MaxSupportDistance, 0.0001f);
            Assert.AreEqual(0.4f, _supportProfile.CollapseDelaySeconds, 0.0001f);
            Assert.IsFalse(_supportProfile.AllowDiagonalSupport);
            Debug.Log("[GearAndDestructionProfileTests] Support profile setters validated.");
        }
    }
}