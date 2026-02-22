using NUnit.Framework;
using UnityEngine;
using ByteWar.Survival;
using System.Collections.Generic;

namespace ByteWar.Tests.EditMode
{
    public class InventoryComponentTests
    {
        private GameObject _gameObject;
        private InventoryComponent _inventory;
        private Item _testItem;

        [SetUp]
        public void Setup()
        {
            _gameObject = new GameObject();
            _inventory = _gameObject.AddComponent<InventoryComponent>();
            _testItem = ScriptableObject.CreateInstance<Item>();
            _testItem.ItemName = "Test Item";
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_gameObject);
            Object.DestroyImmediate(_testItem);
        }

        [Test]
        public void AddItem_IncreasesItemCount()
        {
            // Arrange
            int initialCount = _inventory.Items.Count;

            // Act
            _inventory.AddItem(_testItem);

            // Assert
            // Note: IsServer check might prevent this in EditMode.
            Assert.AreEqual(initialCount + 1, _inventory.Items.Count);
            Debug.Log("AddItem_IncreasesItemCount test executed.");
        }

        [Test]
        public void HasItem_ReturnsTrue_WhenItemExists()
        {
            // Arrange
            _inventory.Items.Add(_testItem);

            // Act
            bool hasItem = _inventory.HasItem(_testItem, 1);

            // Assert
            Assert.IsTrue(hasItem);
            Debug.Log("HasItem_ReturnsTrue_WhenItemExists test executed.");
        }

        [Test]
        public void HasItem_ReturnsFalse_WhenItemDoesNotExist()
        {
            // Arrange
            // Empty inventory

            // Act
            bool hasItem = _inventory.HasItem(_testItem, 1);

            // Assert
            Assert.IsFalse(hasItem);
            Debug.Log("HasItem_ReturnsFalse_WhenItemDoesNotExist test executed.");
        }

        [Test]
        public void RemoveItem_DecreasesItemCount()
        {
            // Arrange
            _inventory.Items.Add(_testItem);
            _inventory.Items.Add(_testItem);
            int initialCount = _inventory.Items.Count;

            // Act
            _inventory.RemoveItem(_testItem, 1);

            // Assert
            // Note: IsServer check might prevent this in EditMode.
            Assert.AreEqual(initialCount - 1, _inventory.Items.Count);
            Debug.Log("RemoveItem_DecreasesItemCount test executed.");
        }
    }
}