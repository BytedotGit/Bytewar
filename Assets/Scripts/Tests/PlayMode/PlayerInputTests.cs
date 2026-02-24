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

        [UnityTest]
        public IEnumerator JumpPressedMidAir_DoesNotQueueJumpOnLanding()
        {
            // Arrange — ground so CharacterController can become grounded during the test.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "TestGround";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(200f, 1f, 200f);

            // Arrange — camera needed for some runtime code paths.
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.AddComponent<Camera>();
            camObj.transform.position = new Vector3(0, 5, -10);
            camObj.transform.rotation = Quaternion.identity;

            _networkManager.StartHost();
            yield return NGOTestHelper.WaitForLocalPlayerReady(_networkManager);

            var playerObj = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
            var inputHandler = playerObj.GetComponent<PlayerInputHandler>();
            var cc = playerObj.GetComponent<CharacterController>();
            Assert.IsNotNull(inputHandler, "PlayerInputHandler should be attached to the player.");
            Assert.IsNotNull(cc, "CharacterController should be attached to the player.");

            // Make test deterministic: disable real keyboard polling.
            inputHandler.SetSimulatedMovement(Vector2.zero);

            // Ensure we start grounded.
            float start = Time.realtimeSinceStartup;
            while (!cc.isGrounded && Time.realtimeSinceStartup - start < 2f)
                yield return null;
            Assert.IsTrue(cc.isGrounded, "Player should start grounded for this test.");

            // Act 1: jump from ground.
            inputHandler.SimulateJumpPress();

            // Wait until we're airborne.
            start = Time.realtimeSinceStartup;
            while (cc.isGrounded && Time.realtimeSinceStartup - start < 1.5f)
                yield return null;
            Assert.IsFalse(cc.isGrounded, "Player should become airborne after jump.");

            // Act 2: press jump while mid-air (this should be discarded, not buffered).
            inputHandler.SimulateJumpPress();

            // Wait until we land.
            start = Time.realtimeSinceStartup;
            while (!cc.isGrounded && Time.realtimeSinceStartup - start < 3f)
                yield return null;
            Assert.IsTrue(cc.isGrounded, "Player should land within timeout.");

            // Assert: we do NOT immediately jump again after landing.
            float landedY = playerObj.transform.position.y;

            // CharacterController.isGrounded can flicker for a frame; use upward displacement to detect a real jump.
            float maxY = landedY;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 0.6f)
            {
                maxY = Mathf.Max(maxY, playerObj.transform.position.y);
                yield return null;
            }

            Assert.LessOrEqual(maxY, landedY + 0.15f,
                $"Mid-air jump press should not queue a second jump on landing. landedY={landedY:0.###} maxY={maxY:0.###}");

            Object.Destroy(camObj);
            Object.Destroy(ground);
        }
    }
}
