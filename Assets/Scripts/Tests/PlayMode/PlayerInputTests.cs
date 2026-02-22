using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;
using ByteWar.Core;
using ByteWar.Networking;

namespace ByteWar.Tests.PlayMode
{
    /// <summary>
    /// Deterministic input tests using PlayerInputHandler simulation API.
    /// Real InputSystem device injection is validated by AutoTester at build time.
    /// </summary>
    public class PlayerInputTests
    {
        private NetworkManager _networkManager;

        [SetUp]
        public void Setup()
        {
            _networkManager = NGOTestHelper.CreateNetworkManager();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NGOTestHelper.CleanUp();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerInputHandler_RegistersMovementInput()
        {
            // Arrange
            _networkManager.StartHost();
            yield return NGOTestHelper.WaitForLocalPlayerReady(_networkManager);

            var playerObj = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
            var inputHandler = playerObj.GetComponent<PlayerInputHandler>();
            Assert.IsNotNull(inputHandler, "PlayerInputHandler should be attached to the player.");

            // Act — simulate forward (W)
            inputHandler.SetSimulatedMovement(new Vector2(0, 1));
            yield return null;

            // Assert
            Assert.AreEqual(new Vector2(0, 1), inputHandler.MovementInput, "Movement input should register forward as (0, 1).");

            // Act — simulate left (A)
            inputHandler.SetSimulatedMovement(new Vector2(-1, 0));
            yield return null;

            // Assert
            Assert.AreEqual(new Vector2(-1, 0), inputHandler.MovementInput, "Movement input should register left as (-1, 0).");

            // Cleanup
            inputHandler.ClearSimulatedMovement();
        }

        [UnityTest]
        public IEnumerator PlayerInteraction_ConsumesInteractAction()
        {
            // Arrange
            _networkManager.StartHost();
            yield return NGOTestHelper.WaitForLocalPlayerReady(_networkManager);

            var playerObj = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
            var inputHandler = playerObj.GetComponent<PlayerInputHandler>();
            Assert.IsNotNull(inputHandler, "PlayerInputHandler should be attached to the player.");

            // Act — simulate interact press and assert immediately
            // (do NOT yield — PlayerInteraction.Update() would consume the flag)
            inputHandler.SimulateInteractPress();

            // Assert
            Assert.IsTrue(inputHandler.InteractTriggered, "InteractTriggered should be true after SimulateInteractPress().");

            bool consumed = inputHandler.ConsumeInteract();
            Assert.IsTrue(consumed, "ConsumeInteract should return true.");
            Assert.IsFalse(inputHandler.InteractTriggered, "InteractTriggered should be false after consumption.");
        }

        [UnityTest]
        public IEnumerator NetworkPlayer_MovesBasedOnInput()
        {
            // Arrange — camera for camera-relative movement
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.AddComponent<Camera>();
            camObj.transform.position = new Vector3(0, 5, -10);
            camObj.transform.rotation = Quaternion.identity;

            _networkManager.StartHost();
            yield return NGOTestHelper.WaitForLocalPlayerReady(_networkManager);

            var playerObj = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
            var inputHandler = playerObj.GetComponent<PlayerInputHandler>();
            Assert.IsNotNull(inputHandler, "PlayerInputHandler should be attached to the player.");

            var initialPosition = playerObj.transform.position;

            // Act — simulate forward movement for several frames
            inputHandler.SetSimulatedMovement(new Vector2(0, 1));
            yield return new WaitForSeconds(0.5f);

            // Assert
            Assert.Greater(playerObj.transform.position.z, initialPosition.z, "Player should have moved forward along the Z axis relative to camera.");

            // Cleanup
            inputHandler.ClearSimulatedMovement();
            Object.Destroy(camObj);
        }
    }
}
