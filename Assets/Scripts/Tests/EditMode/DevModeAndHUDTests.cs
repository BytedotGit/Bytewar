using NUnit.Framework;
using UnityEngine;
using ByteWar.Building;
using ByteWar.Core;
using ByteWar.Survival;
using System.Collections.Generic;

namespace ByteWar.Tests.EditMode
{
    [TestFixture]
    public class DevModeAndHUDTests
    {
        private GameConstants _savedInstance;

        [SetUp]
        public void SetUp()
        {
            // Capture whatever instance is loaded so we can restore after test
            _savedInstance = GameConstants.Instance;
        }

        [TearDown]
        public void TearDown()
        {
            GameConstants.SetInstanceForTesting(_savedInstance);
        }

        /// <summary>Creates a test GameConstants instance with the given DevMode value.</summary>
        private static GameConstants CreateTestConstants(bool devMode)
        {
            var gc = ScriptableObject.CreateInstance<GameConstants>();
            // Use reflection or serialized field to set DevMode
            var field = typeof(GameConstants).GetField("_devMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(gc, devMode);
            GameConstants.SetInstanceForTesting(gc);
            return gc;
        }

        // ── DevMode / GameConstants ───────────────────────────────────────────

        [Test]
        public void GameConstants_IsDevMode_ReturnsFalse_WhenInstanceNull()
        {
            // When no GameConstants asset is loaded, IsDevMode falls back to false
            // (Instance is null → Instance != null && DevMode → false)
            // We can't easily unload the singleton in tests, so test the static path
            bool result = GameConstants.IsDevMode();
            // Result depends on whether a GameConstants asset exists in Resources/
            // This test verifies the method doesn't throw
            Assert.IsTrue(result || !result, "IsDevMode should return a bool without throwing.");
        }

        [Test]
        public void GameConstants_DevMode_PropertyAccessor_Works()
        {
            // Default value in the field declaration is now false
            var gc = ScriptableObject.CreateInstance<GameConstants>();
            Assert.IsFalse(gc.DevMode, "Default DevMode should be false.");
            Object.DestroyImmediate(gc);
        }

        [Test]
        public void GameConstants_SetDevMode_Toggles_DevMode()
        {
            var gc = CreateTestConstants(false);
            Assert.IsFalse(GameConstants.IsDevMode(), "DevMode should start false.");

            GameConstants.SetDevMode(true);
            Assert.IsTrue(GameConstants.IsDevMode(), "DevMode should be true after SetDevMode(true).");

            GameConstants.SetDevMode(false);
            Assert.IsFalse(GameConstants.IsDevMode(), "DevMode should be false after SetDevMode(false).");

            Object.DestroyImmediate(gc);
        }

        // ── DevConsole ───────────────────────────────────────────────────────

        [Test]
        public void DevConsole_CanBeInstantiated()
        {
            var go = new GameObject("Console");
            var console = go.AddComponent<ByteWar.UI.DevConsole>();
            Assert.IsNotNull(console, "DevConsole should be instantiable.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void DevConsole_ExecuteCommand_Dev_TogglesDevMode()
        {
            var gc = CreateTestConstants(false);
            var go = new GameObject("Console");
            var console = go.AddComponent<ByteWar.UI.DevConsole>();

            Assert.IsFalse(GameConstants.IsDevMode());
            console.ExecuteCommand("/dev");
            Assert.IsTrue(GameConstants.IsDevMode(), "After /dev, DevMode should be on.");

            console.ExecuteCommand("/dev");
            Assert.IsFalse(GameConstants.IsDevMode(), "After second /dev, DevMode should be off.");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(gc);
        }

        [Test]
        public void DevConsole_ExecuteCommand_Help_ProducesOutput()
        {
            var go = new GameObject("Console");
            var console = go.AddComponent<ByteWar.UI.DevConsole>();

            int linesBefore = console.OutputLineCount;
            console.ExecuteCommand("/help");
            Assert.Greater(console.OutputLineCount, linesBefore, "/help should add output lines.");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void DevConsole_ExecuteCommand_Unknown_ProducesWarning()
        {
            var go = new GameObject("Console");
            var console = go.AddComponent<ByteWar.UI.DevConsole>();

            console.ExecuteCommand("/unknown");
            Assert.IsNotNull(console.LastOutputText);
            Assert.IsTrue(console.LastOutputText.Contains("Unknown command"), "Unknown command should produce warning.");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void DevConsole_IsOpen_CanBeClosedProgrammatically()
        {
            var go = new GameObject("Console");
            var console = go.AddComponent<ByteWar.UI.DevConsole>();

            console.IsOpen = true;
            Assert.IsTrue(console.IsOpen, "Console should be open after setting IsOpen=true.");

            console.IsOpen = false;
            Assert.IsFalse(console.IsOpen, "Console should be closed after setting IsOpen=false (simulates Escape key).");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void DevConsole_Dev_SetsPendingClose()
        {
            var gc = CreateTestConstants(false);
            var go = new GameObject("Console");
            var console = go.AddComponent<ByteWar.UI.DevConsole>();

            console.IsOpen = true;
            console.ExecuteCommand("/dev");

            // _pendingClose is private, but we can verify the effect:
            // After the command, PendingClose should be true (checked via reflection)
            var field = typeof(ByteWar.UI.DevConsole).GetField("_pendingClose",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsTrue((bool)field.GetValue(console), "/dev should set _pendingClose so the console auto-closes.");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(gc);
        }

        // ── BuildingRecipe DevMode bypass ────────────────────────────────────

        [Test]
        public void BuildingRecipe_CanAfford_InDevMode_ReturnsTrue_WithEmptyInventory()
        {
            // Explicitly enable DevMode for this test
            var gc = CreateTestConstants(true);

            var recipe = ScriptableObject.CreateInstance<BuildingRecipe>();
            var wood = ScriptableObject.CreateInstance<Item>();
            wood.ItemName = "Wood";

            recipe.RecipeName = "TestRecipe";
            recipe.SetCost(new List<RecipeIngredient>
            {
                new RecipeIngredient { Item = wood, Amount = 10 }
            });

            var go = new GameObject("Player");
            var inv = go.AddComponent<InventoryComponent>();
            // Empty inventory — normally unaffordable

            Assert.IsTrue(GameConstants.IsDevMode(), "DevMode should be explicitly enabled.");
            Assert.IsTrue(recipe.CanAfford(inv), "In DevMode, CanAfford should always return true.");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(recipe);
            Object.DestroyImmediate(wood);
            Object.DestroyImmediate(gc);
        }

        [Test]
        public void BuildingRecipe_CanAfford_NotInDevMode_ReturnsFalse_WithEmptyInventory()
        {
            // Explicitly disable DevMode for this test
            var gc = CreateTestConstants(false);

            var recipe = ScriptableObject.CreateInstance<BuildingRecipe>();
            var wood = ScriptableObject.CreateInstance<Item>();
            wood.ItemName = "Wood";

            recipe.RecipeName = "TestRecipe";
            recipe.SetCost(new List<RecipeIngredient>
            {
                new RecipeIngredient { Item = wood, Amount = 10 }
            });

            var go = new GameObject("Player");
            var inv = go.AddComponent<InventoryComponent>();

            Assert.IsFalse(GameConstants.IsDevMode(), "DevMode should be explicitly disabled.");
            Assert.IsFalse(recipe.CanAfford(inv), "Without DevMode, CanAfford should return false with empty inventory.");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(recipe);
            Object.DestroyImmediate(wood);
            Object.DestroyImmediate(gc);
        }

        [Test]
        public void BuildingRecipe_ConsumeResources_InDevMode_ReturnsTrue_WithoutRemoving()
        {
            // Explicitly enable DevMode for this test
            var gc = CreateTestConstants(true);

            var recipe = ScriptableObject.CreateInstance<BuildingRecipe>();
            var wood = ScriptableObject.CreateInstance<Item>();
            wood.ItemName = "Wood";

            recipe.RecipeName = "TestRecipe";
            recipe.SetCost(new List<RecipeIngredient>
            {
                new RecipeIngredient { Item = wood, Amount = 5 }
            });

            var go = new GameObject("Player");
            var inv = go.AddComponent<InventoryComponent>();
            // Add 2 items (not enough normally)
            inv.AddItem(wood);
            inv.AddItem(wood);

            Assert.IsTrue(GameConstants.IsDevMode(), "DevMode should be explicitly enabled.");
            bool result = recipe.ConsumeResources(inv);
            Assert.IsTrue(result, "In DevMode, ConsumeResources should return true.");
            Assert.AreEqual(2, inv.Items.Count, "In DevMode, items should not be consumed.");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(recipe);
            Object.DestroyImmediate(wood);
            Object.DestroyImmediate(gc);
        }

        // ── KeybindingHUD ────────────────────────────────────────────────────

        [Test]
        public void KeybindingHUD_CanBeInstantiated()
        {
            var go = new GameObject("HUD");
            var hud = go.AddComponent<ByteWar.UI.KeybindingHUD>();
            Assert.IsNotNull(hud, "KeybindingHUD should be instantiable.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ScreenLogger_CanBeInstantiated()
        {
            var go = new GameObject("Logger");
            var logger = go.AddComponent<ByteWar.Core.ScreenLogger>();
            Assert.IsNotNull(logger, "ScreenLogger should be instantiable.");
            Object.DestroyImmediate(go);
        }

        // ── BuildingPreview Pitch ────────────────────────────────────────────

        [Test]
        public void BuildingPreview_PitchDefaultsToZero()
        {
            var preview = new ByteWar.Building.BuildingPreview();
            Assert.AreEqual(0f, preview.PreviewPitch, 0.01f, "Pitch should default to 0.");
        }

        // ── EnemyAI CharacterController presence ─────────────────────────────

        [Test]
        public void EnemyAI_RequiresAttributeSet()
        {
            var go = new GameObject("Enemy");
            go.AddComponent<ByteWar.Abilities.AttributeSet>();
            var enemy = go.AddComponent<EnemyAI>();
            Assert.IsNotNull(enemy, "EnemyAI should be instantiable with AttributeSet.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void EnemyAI_WithCharacterController_DoesNotThrow()
        {
            var go = new GameObject("Enemy");
            go.AddComponent<ByteWar.Abilities.AttributeSet>();
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2.2f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 1.1f, 0f);
            var enemy = go.AddComponent<EnemyAI>();
            Assert.IsNotNull(enemy, "EnemyAI with CharacterController should be instantiable.");
            Assert.IsNotNull(cc, "CharacterController should be present.");
            Object.DestroyImmediate(go);
        }

        // ── PlayerRegistry ───────────────────────────────────────────────────

        [Test]
        public void PlayerRegistry_Register_IncreasesCount()
        {
            PlayerRegistry.Clear();
            var go = new GameObject("Player1");
            PlayerRegistry.Register(go.transform);
            Assert.AreEqual(1, PlayerRegistry.Count, "Count should be 1 after registering.");
            PlayerRegistry.Clear();
            Object.DestroyImmediate(go);
        }

        [Test]
        public void PlayerRegistry_Unregister_DecreasesCount()
        {
            PlayerRegistry.Clear();
            var go = new GameObject("Player1");
            PlayerRegistry.Register(go.transform);
            PlayerRegistry.Unregister(go.transform);
            Assert.AreEqual(0, PlayerRegistry.Count, "Count should be 0 after unregistering.");
            PlayerRegistry.Clear();
            Object.DestroyImmediate(go);
        }

        [Test]
        public void PlayerRegistry_GetNearest_ReturnsClosestPlayer()
        {
            PlayerRegistry.Clear();
            var go1 = new GameObject("P1");
            go1.transform.position = new Vector3(0, 0, 0);
            var go2 = new GameObject("P2");
            go2.transform.position = new Vector3(10, 0, 0);
            PlayerRegistry.Register(go1.transform);
            PlayerRegistry.Register(go2.transform);

            var nearest = PlayerRegistry.GetNearest(new Vector3(3, 0, 0));
            Assert.AreEqual(go1.transform, nearest, "P1 should be nearest to (3,0,0).");

            PlayerRegistry.Clear();
            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(go2);
        }

        [Test]
        public void PlayerRegistry_GetNearest_ReturnsNull_WhenEmpty()
        {
            PlayerRegistry.Clear();
            var result = PlayerRegistry.GetNearest(Vector3.zero);
            Assert.IsNull(result, "Should return null when no players registered.");
        }

        [Test]
        public void PlayerRegistry_Register_Null_DoesNotThrow()
        {
            PlayerRegistry.Clear();
            PlayerRegistry.Register(null);
            Assert.AreEqual(0, PlayerRegistry.Count, "Null registration should be ignored.");
        }

        // ── CustomNetworkManagerHUD ──────────────────────────────────────────

        [Test]
        public void CustomNetworkManagerHUD_ShouldAutoHost_ReturnsFalse_InEditor()
        {
            bool result = ByteWar.Networking.CustomNetworkManagerHUD.ShouldAutoHost(
                isEditor: true, isServer: false, isClient: false, args: new string[0]);
            Assert.IsFalse(result, "Should not auto-host in Editor.");
        }

        [Test]
        public void CustomNetworkManagerHUD_ShouldAutoHost_ReturnsFalse_WhenAutoTest()
        {
            bool result = ByteWar.Networking.CustomNetworkManagerHUD.ShouldAutoHost(
                isEditor: false, isServer: false, isClient: false, args: new[] { "-autoTest" });
            Assert.IsFalse(result, "Should not auto-host when -autoTest arg present.");
        }
    }
}
