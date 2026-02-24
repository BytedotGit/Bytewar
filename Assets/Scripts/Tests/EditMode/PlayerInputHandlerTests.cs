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
        public void MovementAction_HasKeyboardBindings_ForWASD()
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

                // InputAction has WASD composite; arrow/AZERTY are handled via direct Keyboard.current polling
                Assert.Contains("<Keyboard>/w", paths, "Expected WASD 'w' binding");
                Assert.Contains("<Keyboard>/a", paths, "Expected WASD 'a' binding");
                Assert.Contains("<Keyboard>/s", paths, "Expected WASD 's' binding");
                Assert.Contains("<Keyboard>/d", paths, "Expected WASD 'd' binding");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PollKeyboard_HandlesNullKeyboardGracefully()
        {
            var go = new GameObject("Test_PollKeyboard_Null");
            try
            {
                var handler = go.AddComponent<PlayerInputHandler>();
                // In EditMode there may be no Keyboard.current — PollKeyboard should not throw.
                MethodInfo pollKb = typeof(PlayerInputHandler).GetMethod("PollKeyboard", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(pollKb, "Expected PlayerInputHandler.PollKeyboard to exist");

                Assert.DoesNotThrow(() => pollKb.Invoke(handler, null),
                    "PollKeyboard should handle null Keyboard.current gracefully");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void InputSuppressed_ZerosAllInput()
        {
            var go = new GameObject("Test_InputSuppressed");
            try
            {
                var handler = go.AddComponent<PlayerInputHandler>();
                handler.SetSimulatedMovement(new Vector2(1f, 1f));
                handler.InputSuppressed = true;

                // Call Update to trigger suppression
                MethodInfo update = typeof(PlayerInputHandler).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                update.Invoke(handler, null);

                Assert.AreEqual(Vector2.zero, handler.MovementInput, "Movement should be zero when suppressed.");
                Assert.IsFalse(handler.InteractTriggered, "Interact should be false when suppressed.");
                Assert.IsFalse(handler.JumpTriggered, "Jump should be false when suppressed.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ForceResetActions_DisablesAndReEnablesActions()
        {
            var go = new GameObject("Test_ForceReset");
            try
            {
                var handler = go.AddComponent<PlayerInputHandler>();

                // In EditMode, Awake/OnEnable don't run automatically — invoke manually
                MethodInfo awake = typeof(PlayerInputHandler).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                awake.Invoke(handler, null);
                MethodInfo onEnable = typeof(PlayerInputHandler).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
                onEnable.Invoke(handler, null);

                // Actions are enabled after Awake+OnEnable
                Assert.IsTrue(handler.MovementAction.enabled, "MovementAction should be enabled before reset.");

                handler.ForceResetActions();

                Assert.IsTrue(handler.MovementAction.enabled, "MovementAction should be re-enabled after ForceResetActions.");
                Assert.IsTrue(handler.CastSpell1Action.enabled, "CastSpell1Action should be re-enabled.");
                Assert.IsTrue(handler.InteractAction.enabled, "InteractAction should be re-enabled.");
                Assert.IsTrue(handler.BuildAction.enabled, "BuildAction should be re-enabled.");
                Assert.IsTrue(handler.JumpAction.enabled, "JumpAction should be re-enabled.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
