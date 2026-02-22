using NUnit.Framework;
using ByteWar.Core;
using System.Reflection;
using UnityEngine;
using System.Linq;

namespace ByteWar.Tests.EditMode
{
    public class PlayerInputHandlerTests
    {
        [Test]
        public void SetSimulatedMovement_SetsMovementInput()
        {
            var go = new GameObject("Test_PlayerInputHandler");
            try
            {
                var handler = go.AddComponent<PlayerInputHandler>();
                handler.SetSimulatedMovement(new Vector2(0.25f, -0.75f));
                Assert.AreEqual(new Vector2(0.25f, -0.75f), handler.MovementInput);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Update_DoesNotOverrideMovement_WhenSimulated()
        {
            var go = new GameObject("Test_PlayerInputHandler_Simulated");
            try
            {
                var handler = go.AddComponent<PlayerInputHandler>();
                handler.SetSimulatedMovement(new Vector2(0f, 1f));

                // Call private Update() via reflection in EditMode.
                MethodInfo update = typeof(PlayerInputHandler).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(update, "Expected PlayerInputHandler.Update to exist");
                update.Invoke(handler, null);

                Assert.AreEqual(new Vector2(0f, 1f), handler.MovementInput);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void MovementAction_HasKeyboardBindings_ForCommonLayouts()
        {
            var go = new GameObject("Test_PlayerInputHandler_Bindings");
            try
            {
                var handler = go.AddComponent<PlayerInputHandler>();

                // In EditMode, Awake is not guaranteed to run immediately on AddComponent.
                MethodInfo awake = typeof(PlayerInputHandler).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(awake, "Expected PlayerInputHandler.Awake to exist");
                awake.Invoke(handler, null);

                Assert.NotNull(handler.MovementAction, "Expected MovementAction to be initialized");

                var paths = handler.MovementAction.bindings.Select(b => b.path).ToArray();

                Assert.Contains("<Keyboard>/w", paths, "Expected WASD 'w' binding");
                Assert.Contains("<Keyboard>/a", paths, "Expected WASD 'a' binding");
                Assert.Contains("<Keyboard>/s", paths, "Expected WASD 's' binding");
                Assert.Contains("<Keyboard>/d", paths, "Expected WASD 'd' binding");

                Assert.Contains("<Keyboard>/upArrow", paths, "Expected arrow key Up binding");
                Assert.Contains("<Keyboard>/leftArrow", paths, "Expected arrow key Left binding");

                // AZERTY fallback
                Assert.Contains("<Keyboard>/z", paths, "Expected ZQSD 'z' binding");
                Assert.Contains("<Keyboard>/q", paths, "Expected ZQSD 'q' binding");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
