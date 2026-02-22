using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;
using SurvivalRPG.Core;
using SurvivalRPG.Networking;
using UnityEngine.InputSystem;

namespace SurvivalRPG.Tests.PlayMode
{
    public class PlayerInputTests
    {
        private NetworkManager _networkManager;
        private GameObject _playerPrefab;

        [SetUp]
        public void Setup()
        {
            _networkManager = NGOTestHelper.CreateNetworkManager();
            _playerPrefab = _networkManager.NetworkConfig.PlayerPrefab;
        }

        [TearDown]
        public void TearDown()
        {
            NGOTestHelper.CleanUp();
        }

        [UnityTest]
        public IEnumerator PlayerInputHandler_RegistersMovementInput()
        {
            // Arrange
            _networkManager.StartHost();
            yield return new WaitForSeconds(0.1f);

            var playerObj = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
            var inputHandler = playerObj.GetComponent<PlayerInputHandler>();

            Assert.IsNotNull(inputHandler, "PlayerInputHandler should be attached to the player.");

            // Act
            var keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));
            InputSystem.Update();
            yield return null; // Wait for input system to process

            // Assert
            Assert.AreEqual(new Vector2(0, 1), inputHandler.MovementInput, "Movement input should register 'W' as (0, 1).");

            // Act
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.A));
            InputSystem.Update();
            yield return null;

            // Assert
            Assert.AreEqual(new Vector2(-1, 0), inputHandler.MovementInput, "Movement input should register 'A' as (-1, 0).");
        }

        [UnityTest]
        public IEnumerator PlayerInteraction_ConsumesInteractAction()
        {
            // Arrange
            _networkManager.StartHost();
            yield return new WaitForSeconds(0.1f);

            var playerObj = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
            var inputHandler = playerObj.GetComponent<PlayerInputHandler>();

            var keyboard = InputSystem.AddDevice<Keyboard>();

            // Act
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.E));
            InputSystem.Update();
            yield return null;

            // Assert
            Assert.IsTrue(inputHandler.InteractTriggered, "InteractTriggered should be true after pressing 'E'.");

            bool consumed = inputHandler.ConsumeInteract();
            Assert.IsTrue(consumed, "ConsumeInteract should return true.");
            Assert.IsFalse(inputHandler.InteractTriggered, "InteractTriggered should be false after consumption.");
        }

        [UnityTest]
        public IEnumerator NetworkPlayer_MovesBasedOnInput()
        {
            // Arrange
            // Create a camera for camera-relative movement
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            camObj.transform.position = new Vector3(0, 5, -10);
            camObj.transform.rotation = Quaternion.identity;

            _networkManager.StartHost();
            yield return new WaitForSeconds(0.1f);

            var playerObj = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
            var initialPosition = playerObj.transform.position;

            var keyboard = InputSystem.AddDevice<Keyboard>();

            // Act
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));
            InputSystem.Update();
            yield return new WaitForSeconds(0.5f); // Wait for movement to apply over time

            // Assert
            Assert.Greater(playerObj.transform.position.z, initialPosition.z, "Player should have moved forward along the Z axis relative to camera.");

            // Cleanup
            Object.Destroy(camObj);
        }
    }
}
