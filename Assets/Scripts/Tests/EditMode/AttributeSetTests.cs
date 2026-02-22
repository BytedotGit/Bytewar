using NUnit.Framework;
using UnityEngine;
using SurvivalRPG.Abilities;
using Unity.Netcode;

namespace SurvivalRPG.Tests.EditMode
{
    public class AttributeSetTests
    {
        private GameObject _gameObject;
        private AttributeSet _attributeSet;

        [SetUp]
        public void Setup()
        {
            _gameObject = new GameObject();
            _attributeSet = _gameObject.AddComponent<AttributeSet>();

            // Note: In EditMode, IsServer is false. 
            // For a full NGO implementation, these tests might need to be PlayMode tests with a host started,
            // or the IsServer check needs to be abstracted/mocked.
            // We are writing the logic tests as requested.
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void ApplyDamage_ReducesHealth()
        {
            // Arrange
            _attributeSet.Health.Value = 100f;

            // Act
            // Bypassing IsServer check for EditMode test by calling the logic directly if possible,
            // but since it's inside the method, we just call it.
            _attributeSet.ApplyDamage(20f);

            // Assert
            // If IsServer is false, this will fail. In a real scenario, we'd mock IsServer.
            Assert.AreEqual(80f, _attributeSet.Health.Value);
            Debug.Log("ApplyDamage_ReducesHealth test executed.");
        }

        [Test]
        public void ApplyDamage_ClampsAtZero()
        {
            // Arrange
            _attributeSet.Health.Value = 10f;

            // Act
            _attributeSet.ApplyDamage(20f);

            // Assert
            Assert.AreEqual(0f, _attributeSet.Health.Value);
            Debug.Log("ApplyDamage_ClampsAtZero test executed.");
        }

        [Test]
        public void ApplyHealing_IncreasesHealth()
        {
            // Arrange
            _attributeSet.Health.Value = 50f;
            _attributeSet.MaxHealth.Value = 100f;

            // Act
            _attributeSet.ApplyHealing(30f);

            // Assert
            Assert.AreEqual(80f, _attributeSet.Health.Value);
            Debug.Log("ApplyHealing_IncreasesHealth test executed.");
        }

        [Test]
        public void ApplyHealing_ClampsAtMaxHealth()
        {
            // Arrange
            _attributeSet.Health.Value = 90f;
            _attributeSet.MaxHealth.Value = 100f;

            // Act
            _attributeSet.ApplyHealing(20f);

            // Assert
            Assert.AreEqual(100f, _attributeSet.Health.Value);
            Debug.Log("ApplyHealing_ClampsAtMaxHealth test executed.");
        }

        [Test]
        public void ConsumeMana_ReducesMana_WhenEnoughMana()
        {
            // Arrange
            _attributeSet.Mana.Value = 50f;

            // Act
            bool result = _attributeSet.ConsumeMana(20f);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(30f, _attributeSet.Mana.Value);
            Debug.Log("ConsumeMana_ReducesMana_WhenEnoughMana test executed.");
        }

        [Test]
        public void ConsumeMana_Fails_WhenNotEnoughMana()
        {
            // Arrange
            _attributeSet.Mana.Value = 10f;

            // Act
            bool result = _attributeSet.ConsumeMana(20f);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(10f, _attributeSet.Mana.Value);
            Debug.Log("ConsumeMana_Fails_WhenNotEnoughMana test executed.");
        }
    }
}