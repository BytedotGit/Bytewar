using NUnit.Framework;
using UnityEngine;
using ByteWar.Building;
using ByteWar.Survival;
using System.Collections.Generic;

namespace ByteWar.Tests.EditMode
{
    [TestFixture]
    public class BuildingSystemTests
    {
        // ── BuildingPiece ─────────────────────────────────────────────────────

        [Test]
        public void BuildingPiece_GetSnapPoints_Foundation_ReturnsEightPoints()
        {
            var go = new GameObject("TestFoundation");
            var piece = go.AddComponent<BuildingPiece>();
            // Default values: Foundation, gridSize=4

            SnapPoint[] points = piece.GetSnapPoints();
            // 4 foundation neighbours + 4 wall positions = 8
            Assert.AreEqual(8, points.Length, "Foundation should have 8 snap points (4 foundation + 4 wall).");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BuildingPiece_GetSnapPoints_Foundation_EdgeToEdge_FullGridOffset()
        {
            var go = new GameObject("TestFoundation");
            go.transform.position = new Vector3(10f, 0f, 20f);
            var piece = go.AddComponent<BuildingPiece>();

            SnapPoint[] points = piece.GetSnapPoints();
            // Foundation snap points should be exactly gridSize (4.0) away for foundation targets
            int foundationCount = 0;
            foreach (var sp in points)
            {
                if (sp.TargetType != BuildingPieceType.Foundation) continue;
                foundationCount++;
                float dist = Vector3.Distance(sp.Position, go.transform.position);
                Assert.AreEqual(4.0f, dist, 0.01f, $"Foundation snap point at {sp.Position} should be 4.0 units from center (edge-to-edge).");
            }
            Assert.AreEqual(4, foundationCount, "Should have 4 foundation snap points.");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void BuildingPiece_GetSnapPoints_WallTargets_AtHalfGrid()
        {
            var go = new GameObject("TestFoundation");
            go.transform.position = Vector3.zero;
            var piece = go.AddComponent<BuildingPiece>();

            SnapPoint[] points = piece.GetSnapPoints();
            int wallCount = 0;
            foreach (var sp in points)
            {
                if (sp.TargetType != BuildingPieceType.Wall) continue;
                wallCount++;
                float dist = Vector3.Distance(sp.Position, go.transform.position);
                Assert.AreEqual(2.0f, dist, 0.01f, $"Wall snap point at {sp.Position} should be 2.0 units from foundation center.");
            }
            Assert.AreEqual(4, wallCount, "Should have 4 wall snap points on a foundation.");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void SnapPoint_WallTargets_HaveAutoOrientation()
        {
            var go = new GameObject("TestFoundation");
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            var piece = go.AddComponent<BuildingPiece>();

            SnapPoint[] points = piece.GetSnapPoints();
            foreach (var sp in points)
            {
                if (sp.TargetType != BuildingPieceType.Wall) continue;
                // Wall rotation should face outward from foundation center:
                // The forward vector of the rotation should point away from center.
                Vector3 snapDir = (sp.Position - go.transform.position).normalized;
                Vector3 rotForward = sp.Rotation * Vector3.forward;
                float dot = Vector3.Dot(snapDir, rotForward);
                Assert.Greater(dot, 0.9f, $"Wall snap at {sp.Position} should face outward (dot={dot:0.00}).");
            }

            Object.DestroyImmediate(go);
        }

        // ── Grid Snapping ─────────────────────────────────────────────────────

        [Test]
        public void SnapToGrid_SnapsCorrectly()
        {
            Vector3 input = new Vector3(5.3f, 1.0f, 7.8f);
            Vector3 result = BuildingSnap.SnapToGrid(input, 4f);
            Assert.AreEqual(4f, result.x, 0.01f, "X should snap to 4");
            Assert.AreEqual(1f, result.y, 0.01f, "Y should not change");
            Assert.AreEqual(8f, result.z, 0.01f, "Z should snap to 8");
        }

        [Test]
        public void SnapToGrid_ZeroGridSize_ReturnsOriginal()
        {
            Vector3 input = new Vector3(3f, 2f, 5f);
            Vector3 result = BuildingSnap.SnapToGrid(input, 0f);
            Assert.AreEqual(input, result, "Grid size 0 should return original position.");
        }

        [Test]
        public void SnapToGrid_NegativePosition_SnapsCorrectly()
        {
            Vector3 input = new Vector3(-5.3f, 0f, -7.8f);
            Vector3 result = BuildingSnap.SnapToGrid(input, 4f);
            Assert.AreEqual(-4f, result.x, 0.01f);
            Assert.AreEqual(-8f, result.z, 0.01f);
        }

        // ── BuildingRecipe ────────────────────────────────────────────────────

        [Test]
        public void BuildingRecipe_CanAfford_ReturnsTrueWhenSufficient()
        {
            var recipe = ScriptableObject.CreateInstance<BuildingRecipe>();
            var wood = ScriptableObject.CreateInstance<Item>();
            wood.ItemName = "Wood";

            recipe.SetCost(new List<RecipeIngredient>
            {
                new RecipeIngredient { Item = wood, Amount = 3 }
            });

            var go = new GameObject("Player");
            var inv = go.AddComponent<InventoryComponent>();
            inv.AddItem(wood);
            inv.AddItem(wood);
            inv.AddItem(wood);

            Assert.IsTrue(recipe.CanAfford(inv));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(recipe);
            Object.DestroyImmediate(wood);
        }

        [Test]
        public void BuildingRecipe_CanAfford_ReturnsFalseWhenInsufficient()
        {
            var recipe = ScriptableObject.CreateInstance<BuildingRecipe>();
            var wood = ScriptableObject.CreateInstance<Item>();
            wood.ItemName = "Wood";

            recipe.SetCost(new List<RecipeIngredient>
            {
                new RecipeIngredient { Item = wood, Amount = 3 }
            });

            var go = new GameObject("Player");
            var inv = go.AddComponent<InventoryComponent>();
            inv.AddItem(wood);

            Assert.IsFalse(recipe.CanAfford(inv));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(recipe);
            Object.DestroyImmediate(wood);
        }

        [Test]
        public void BuildingRecipe_CanAfford_NullInventory_ReturnsFalse()
        {
            var recipe = ScriptableObject.CreateInstance<BuildingRecipe>();
            recipe.SetCost(new List<RecipeIngredient>());

            Assert.IsFalse(recipe.CanAfford(null));

            Object.DestroyImmediate(recipe);
        }

        [Test]
        public void BuildingRecipe_ConsumeResources_RemovesItems()
        {
            var recipe = ScriptableObject.CreateInstance<BuildingRecipe>();
            var wood = ScriptableObject.CreateInstance<Item>();
            wood.ItemName = "Wood";

            recipe.RecipeName = "TestRecipe";
            recipe.SetCost(new List<RecipeIngredient>
            {
                new RecipeIngredient { Item = wood, Amount = 2 }
            });

            var go = new GameObject("Player");
            var inv = go.AddComponent<InventoryComponent>();
            inv.AddItem(wood);
            inv.AddItem(wood);
            inv.AddItem(wood);

            bool result = recipe.ConsumeResources(inv);
            Assert.IsTrue(result);
            Assert.AreEqual(1, inv.Items.Count, "Should have 1 wood remaining.");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(recipe);
            Object.DestroyImmediate(wood);
        }

        // ── WorldPersistence (serialization) ──────────────────────────────────

        [Test]
        public void WorldSaveData_SerializesToJson()
        {
            var data = new WorldSaveData();
            data.SavedAtUtc = "2025-01-01T00:00:00Z";
            data.Buildings.Add(new BuildingSaveEntry
            {
                PieceType = (int)BuildingPieceType.Foundation,
                PosX = 10f,
                PosY = 0f,
                PosZ = 20f,
                RotY = 90f,
                PlacedByClientId = 42,
            });

            string json = JsonUtility.ToJson(data);
            Assert.IsFalse(string.IsNullOrEmpty(json));

            var deserialized = JsonUtility.FromJson<WorldSaveData>(json);
            Assert.AreEqual(1, deserialized.Buildings.Count);
            Assert.AreEqual(10f, deserialized.Buildings[0].PosX, 0.01f);
            Assert.AreEqual(90f, deserialized.Buildings[0].RotY, 0.01f);
            Assert.AreEqual(42UL, deserialized.Buildings[0].PlacedByClientId);
        }

        [Test]
        public void WorldSaveData_EmptyBuildings_SerializesCorrectly()
        {
            var data = new WorldSaveData();
            data.SavedAtUtc = "test";

            string json = JsonUtility.ToJson(data);
            var deserialized = JsonUtility.FromJson<WorldSaveData>(json);
            Assert.IsNotNull(deserialized.Buildings);
            Assert.AreEqual(0, deserialized.Buildings.Count);
        }

        [Test]
        public void WorldSaveData_MultipleBuildings_RoundTrips()
        {
            var data = new WorldSaveData();
            data.SavedAtUtc = "now";
            data.Buildings.Add(new BuildingSaveEntry
            {
                PieceType = (int)BuildingPieceType.Foundation,
                PosX = 0,
                PosY = 0,
                PosZ = 0,
                RotY = 0,
                PlacedByClientId = 1
            });
            data.Buildings.Add(new BuildingSaveEntry
            {
                PieceType = (int)BuildingPieceType.Wall,
                PosX = 4,
                PosY = 0,
                PosZ = 0,
                RotY = 180,
                PlacedByClientId = 2
            });

            string json = JsonUtility.ToJson(data);
            var result = JsonUtility.FromJson<WorldSaveData>(json);
            Assert.AreEqual(2, result.Buildings.Count);
            Assert.AreEqual((int)BuildingPieceType.Wall, result.Buildings[1].PieceType);
            Assert.AreEqual(180f, result.Buildings[1].RotY, 0.01f);
        }
    }
}
