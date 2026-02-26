using NUnit.Framework;
using UnityEngine;
using ByteWar.Building;
using ByteWar.Core;
using ByteWar.Survival;
using System.Collections.Generic;
using ByteWar.Editor;

namespace ByteWar.Tests.EditMode
{
    [TestFixture]
    public class BuildingSystemTests
    {
        private GameConstants _savedInstance;

        /// <summary>Inject a non-DevMode GameConstants so cost tests behave deterministically.</summary>
        private void DisableDevMode()
        {
            _savedInstance = GameConstants.Instance;
            var gc = ScriptableObject.CreateInstance<GameConstants>();
            var devModeField = typeof(GameConstants).GetField("_devMode",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            devModeField.SetValue(gc, false);
            var costsField = typeof(GameConstants).GetField("_buildingCostsEnabled",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            costsField.SetValue(gc, true);
            GameConstants.SetInstanceForTesting(gc);
        }

        private void RestoreDevMode()
        {
            GameConstants.SetInstanceForTesting(_savedInstance);
        }

        [Test]
        public void BuildingController_InternalBuildToggle_DefaultsToFalse()
        {
            var go = new GameObject("Test_BuildingController_DefaultToggle");
            try
            {
                var controller = go.AddComponent<BuildingController>();
                var field = typeof(BuildingController).GetField("_internalBuildToggleInputEnabled",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                Assert.NotNull(field, "Expected _internalBuildToggleInputEnabled field to exist.");
                bool value = (bool)field.GetValue(controller);
                Assert.IsFalse(value, "BuildingController should default internal B toggle input to false so hold-B radial owns B.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DeveloperPlacementAllowlistHelper_ReturnsTrue_ForAllowlistedPath()
        {
            var method = typeof(BuildingController).GetMethod(
                "IsDeveloperPlacementPathAllowlisted",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(HashSet<string>) },
                null);

            Assert.NotNull(method, "Expected private allowlist helper to exist.");

            var allowlist = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "Generated/BlenderE2EProp/BlenderE2EProp"
            };

            bool allowed = (bool)method.Invoke(
                null,
                new object[] { "generated/blendere2eprop/blendere2eprop", allowlist });

            Assert.IsTrue(allowed, "Allowlisted developer resource path should be accepted.");
        }

        [Test]
        public void DeveloperPlacementAllowlistHelper_ReturnsFalse_ForMissingPath()
        {
            var method = typeof(BuildingController).GetMethod(
                "IsDeveloperPlacementPathAllowlisted",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(HashSet<string>) },
                null);

            Assert.NotNull(method, "Expected private allowlist helper to exist.");

            var allowlist = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "Generated/BlenderE2EProp/BlenderE2EProp"
            };

            bool allowed = (bool)method.Invoke(
                null,
                new object[] { "Generated/NotAllowlisted/Prefab", allowlist });

            Assert.IsFalse(allowed, "Non-allowlisted developer resource path should be rejected.");
        }

        // ── Helper: create a piece with SnapPointMarker children ──────────────

        private static GameObject CreatePieceWithSnaps(BuildingPieceType type, Vector3 position,
            params Vector3[] snapLocals)
        {
            var go = new GameObject($"Test_{type}");
            go.transform.position = position;
            var piece = go.AddComponent<BuildingPiece>();
            var field = typeof(BuildingPiece).GetField("_pieceType",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(piece, type);
            // Add BoxCollider for OverlapSphere detection
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(4f, 3f, 4f);
            col.center = new Vector3(0f, 1.5f, 0f);

            foreach (var local in snapLocals)
            {
                var child = new GameObject($"Snap");
                child.transform.SetParent(go.transform);
                child.transform.localPosition = local;
                child.AddComponent<SnapPointMarker>();
            }
            return go;
        }

        // ── SnapPointMarker Tests ─────────────────────────────────────────────

        [Test]
        public void SnapPointMarker_WorldPosition_MatchesTransform()
        {
            var parent = new GameObject("Parent");
            parent.transform.position = new Vector3(10f, 2f, 5f);
            var child = new GameObject("Snap");
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = new Vector3(2f, 0.4f, 2f);
            var marker = child.AddComponent<SnapPointMarker>();

            Assert.AreEqual(12f, marker.WorldPosition.x, 0.01f);
            Assert.AreEqual(2.4f, marker.WorldPosition.y, 0.01f);
            Assert.AreEqual(7f, marker.WorldPosition.z, 0.01f);

            Object.DestroyImmediate(parent);
        }

        [Test]
        public void SnapPointMarker_LocalPosition_ReturnsLocalCoords()
        {
            var parent = new GameObject("Parent");
            var child = new GameObject("Snap");
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = new Vector3(-2f, 3f, 0.15f);
            var marker = child.AddComponent<SnapPointMarker>();

            Assert.AreEqual(-2f, marker.LocalPosition.x, 0.01f);
            Assert.AreEqual(3f, marker.LocalPosition.y, 0.01f);
            Assert.AreEqual(0.15f, marker.LocalPosition.z, 0.01f);

            Object.DestroyImmediate(parent);
        }

        // ── Snap Point Count Tests per Piece Type ─────────────────────────────

        [Test]
        public void Foundation_ShouldHave8SnapPoints()
        {
            // Foundation: 4 corners + 4 edge midpoints
            var go = CreatePieceWithSnaps(BuildingPieceType.Foundation, Vector3.zero,
                new Vector3(-2f, 0.4f, -2f), new Vector3(-2f, 0.4f, 2f),
                new Vector3(2f, 0.4f, -2f), new Vector3(2f, 0.4f, 2f),
                new Vector3(0f, 0.4f, -2f), new Vector3(0f, 0.4f, 2f),
                new Vector3(-2f, 0.4f, 0f), new Vector3(2f, 0.4f, 0f));
            var markers = go.GetComponentsInChildren<SnapPointMarker>();
            Assert.AreEqual(8, markers.Length, "Foundation should have 8 snap points.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Wall_ShouldHave4SnapPoints()
        {
            var go = CreatePieceWithSnaps(BuildingPieceType.Wall, Vector3.zero,
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f),
                new Vector3(-2f, 3f, 0f), new Vector3(2f, 3f, 0f));
            var markers = go.GetComponentsInChildren<SnapPointMarker>();
            Assert.AreEqual(4, markers.Length, "Wall should have 4 centerline snap points.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Floor_ShouldHave8SnapPoints()
        {
            var go = CreatePieceWithSnaps(BuildingPieceType.Floor, Vector3.zero,
                new Vector3(-2f, 0f, -2f), new Vector3(-2f, 0f, 2f),
                new Vector3(2f, 0f, -2f), new Vector3(2f, 0f, 2f),
                new Vector3(0f, 0f, -2f), new Vector3(0f, 0f, 2f),
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f));
            var markers = go.GetComponentsInChildren<SnapPointMarker>();
            Assert.AreEqual(8, markers.Length, "Floor should have 8 snap points.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Ramp_ShouldHave6SnapPoints()
        {
            var go = CreatePieceWithSnaps(BuildingPieceType.Ramp, Vector3.zero,
                new Vector3(-2f, 0f, -2f), new Vector3(2f, 0f, -2f),
                new Vector3(-2f, 3f, 2f), new Vector3(2f, 3f, 2f),
                new Vector3(-2f, 1.5f, 0f), new Vector3(2f, 1.5f, 0f));
            var markers = go.GetComponentsInChildren<SnapPointMarker>();
            Assert.AreEqual(6, markers.Length, "Ramp should have 6 snap points.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Roof26_ShouldHave10SnapPoints()
        {
            float ridgeH = Mathf.Tan(26f * Mathf.Deg2Rad) * 2f;
            var go = CreatePieceWithSnaps(BuildingPieceType.Roof26, Vector3.zero,
                new Vector3(-2f, 0f, -2f), new Vector3(-2f, 0f, 2f),
                new Vector3(2f, 0f, -2f), new Vector3(2f, 0f, 2f),
                new Vector3(0f, 0f, -2f), new Vector3(0f, 0f, 2f),
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f),
                new Vector3(0f, ridgeH, -2f), new Vector3(0f, ridgeH, 2f));
            var markers = go.GetComponentsInChildren<SnapPointMarker>();
            Assert.AreEqual(10, markers.Length, "Roof26 should have 10 snap points (4 corners + 4 edge mids + 2 ridge).");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Stairs_ShouldHave6SnapPoints()
        {
            var go = CreatePieceWithSnaps(BuildingPieceType.Stairs, Vector3.zero,
                new Vector3(-2f, 0f, -2f), new Vector3(2f, 0f, -2f),
                new Vector3(0f, 0f, -2f),
                new Vector3(-2f, 3f, 2f), new Vector3(2f, 3f, 2f),
                new Vector3(0f, 3f, 2f));
            var markers = go.GetComponentsInChildren<SnapPointMarker>();
            Assert.AreEqual(6, markers.Length, "Stairs should have 6 snap points (2 bottom corners + bottom mid + 2 top corners + top mid).");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Pole_ShouldHave2SnapPoints()
        {
            var go = CreatePieceWithSnaps(BuildingPieceType.Pole, Vector3.zero,
                new Vector3(0f, 0f, 0f), new Vector3(0f, 3f, 0f));
            var markers = go.GetComponentsInChildren<SnapPointMarker>();
            Assert.AreEqual(2, markers.Length, "Pole should have 2 snap points.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Beam_ShouldHave2SnapPoints()
        {
            var go = CreatePieceWithSnaps(BuildingPieceType.Beam, Vector3.zero,
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f));
            var markers = go.GetComponentsInChildren<SnapPointMarker>();
            Assert.AreEqual(2, markers.Length, "Beam should have 2 snap points.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void AngledWall_ShouldHave3SnapPoints()
        {
            float peakH = Mathf.Tan(26f * Mathf.Deg2Rad) * 2f;
            var go = CreatePieceWithSnaps(BuildingPieceType.AngledWall, Vector3.zero,
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f),
                new Vector3(0f, peakH, 0f));
            var markers = go.GetComponentsInChildren<SnapPointMarker>();
            Assert.AreEqual(3, markers.Length, "AngledWall should have 3 snap points.");
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

        // ── TryAdjacencySnap (nearest-pair) ──────────────────────────────────

        [Test]
        public void TryAdjacencySnap_SnapsWhenWithinRadius()
        {
            // Place a foundation at origin with a snap point at (2, 0.4, 0)
            var placed = CreatePieceWithSnaps(BuildingPieceType.Foundation,
                Vector3.zero, new Vector3(2f, 0.4f, 0f));

            // Preview piece has a snap point at (-2, 0, 0) — should align them
            var previewLocals = new List<Vector3> { new Vector3(-2f, 0f, 0f) };
            Vector3 cursorPos = new Vector3(5f, 0.4f, 0f); // close to the snap
            Quaternion rot = Quaternion.identity;
            var buffer = new Collider[32];

            // Force physics to pick up the collider
            Physics.SyncTransforms();

            bool snapped = BuildingSnap.TryAdjacencySnap(ref cursorPos, rot, previewLocals, 5f, buffer);

            Assert.IsTrue(snapped, "Should snap when within radius.");
            // Expected: placed snap at (2,0.4,0), preview local at (-2,0,0)
            // newPos = (2,0.4,0) - identity*(-2,0,0) = (4, 0.4, 0)
            Assert.AreEqual(4f, cursorPos.x, 0.1f, "X should align snap points.");
            Assert.AreEqual(0.4f, cursorPos.y, 0.1f, "Y should align snap points.");

            Object.DestroyImmediate(placed);
        }

        [Test]
        public void TryAdjacencySnap_NoSnapWhenTooFar()
        {
            var placed = CreatePieceWithSnaps(BuildingPieceType.Foundation,
                Vector3.zero, new Vector3(2f, 0.4f, 0f));

            var previewLocals = new List<Vector3> { new Vector3(-2f, 0f, 0f) };
            Vector3 cursorPos = new Vector3(50f, 0f, 0f); // way too far
            Quaternion rot = Quaternion.identity;
            var buffer = new Collider[32];
            Physics.SyncTransforms();

            bool snapped = BuildingSnap.TryAdjacencySnap(ref cursorPos, rot, previewLocals, 5f, buffer);

            Assert.IsFalse(snapped, "Should not snap when too far away.");
            Assert.AreEqual(50f, cursorPos.x, 0.01f, "Position should be unchanged.");

            Object.DestroyImmediate(placed);
        }

        [Test]
        public void TryAdjacencySnap_EmptySnapLocals_ReturnsFalse()
        {
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            var buffer = new Collider[32];
            bool snapped = BuildingSnap.TryAdjacencySnap(ref pos, rot, new List<Vector3>(), 5f, buffer);
            Assert.IsFalse(snapped);
        }

        [Test]
        public void TryAdjacencySnap_NullSnapLocals_ReturnsFalse()
        {
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            var buffer = new Collider[32];
            bool snapped = BuildingSnap.TryAdjacencySnap(ref pos, rot, null, 5f, buffer);
            Assert.IsFalse(snapped);
        }

        // ── CheckSupport Tests ────────────────────────────────────────────────

        [Test]
        public void CheckSupport_Foundation_AlwaysTrue()
        {
            bool result = BuildingSnap.CheckSupport(Vector3.zero, BuildingPieceType.Foundation, false, 4f, new Collider[32]);
            Assert.IsTrue(result, "Foundation always places freely.");
        }

        [Test]
        public void CheckSupport_Wall_NeedsFoundationOrFloorOrWall()
        {
            // Not snapped → false
            Assert.IsFalse(BuildingSnap.CheckSupport(Vector3.zero, BuildingPieceType.Wall, false, 4f, new Collider[32]));

            // With foundation nearby
            var foundation = CreatePieceWithSnaps(BuildingPieceType.Foundation, Vector3.zero);
            Physics.SyncTransforms();
            bool result = BuildingSnap.CheckSupport(Vector3.zero, BuildingPieceType.Wall, true, 4f, new Collider[32]);
            Assert.IsTrue(result, "Wall should be supported by Foundation.");
            Object.DestroyImmediate(foundation);
        }

        [Test]
        public void CheckSupport_Wall_SupportedByWall()
        {
            var wall = CreatePieceWithSnaps(BuildingPieceType.Wall, Vector3.zero);
            Physics.SyncTransforms();
            bool result = BuildingSnap.CheckSupport(new Vector3(0f, 3f, 0f), BuildingPieceType.Wall, true, 4f, new Collider[32]);
            Assert.IsTrue(result, "Wall should be supported by another Wall (multi-story).");
            Object.DestroyImmediate(wall);
        }

        [Test]
        public void CheckSupport_Wall_SupportedByFloor()
        {
            var floor = CreatePieceWithSnaps(BuildingPieceType.Floor, Vector3.zero);
            Physics.SyncTransforms();
            bool result = BuildingSnap.CheckSupport(Vector3.zero, BuildingPieceType.Wall, true, 4f, new Collider[32]);
            Assert.IsTrue(result, "Wall should be supported by Floor.");
            Object.DestroyImmediate(floor);
        }

        [Test]
        public void CheckSupport_Roof_SupportedByWall()
        {
            var wall = CreatePieceWithSnaps(BuildingPieceType.Wall, Vector3.zero);
            Physics.SyncTransforms();
            bool result = BuildingSnap.CheckSupport(Vector3.zero, BuildingPieceType.Roof26, true, 4f, new Collider[32]);
            Assert.IsTrue(result, "Roof should be supported by Wall.");
            Object.DestroyImmediate(wall);
        }

        [Test]
        public void CheckSupport_Roof_SupportedByFoundation()
        {
            var foundation = CreatePieceWithSnaps(BuildingPieceType.Foundation, Vector3.zero);
            Physics.SyncTransforms();
            bool result = BuildingSnap.CheckSupport(Vector3.zero, BuildingPieceType.Roof26, true, 4f, new Collider[32]);
            Assert.IsTrue(result, "Roof should be supported by Foundation.");
            Object.DestroyImmediate(foundation);
        }

        [Test]
        public void CheckSupport_Ramp_SupportedByWall()
        {
            var wall = CreatePieceWithSnaps(BuildingPieceType.Wall, Vector3.zero);
            Physics.SyncTransforms();
            bool result = BuildingSnap.CheckSupport(Vector3.zero, BuildingPieceType.Ramp, true, 4f, new Collider[32]);
            Assert.IsTrue(result, "Ramp should be supported by Wall.");
            Object.DestroyImmediate(wall);
        }

        [Test]
        public void CheckSupport_Stairs_SupportedByWall()
        {
            var wall = CreatePieceWithSnaps(BuildingPieceType.Wall, Vector3.zero);
            Physics.SyncTransforms();
            bool result = BuildingSnap.CheckSupport(Vector3.zero, BuildingPieceType.Stairs, true, 4f, new Collider[32]);
            Assert.IsTrue(result, "Stairs should be supported by Wall.");
            Object.DestroyImmediate(wall);
        }

        [Test]
        public void CheckSupport_Pole_SupportedByAnyPiece()
        {
            var foundation = CreatePieceWithSnaps(BuildingPieceType.Foundation, Vector3.zero);
            Physics.SyncTransforms();
            bool result = BuildingSnap.CheckSupport(Vector3.zero, BuildingPieceType.Pole, true, 4f, new Collider[32]);
            Assert.IsTrue(result, "Pole should be supported by any piece.");
            Object.DestroyImmediate(foundation);
        }

        [Test]
        public void CheckSupport_Beam_SupportedByAnyPiece()
        {
            var wall = CreatePieceWithSnaps(BuildingPieceType.Wall, Vector3.zero);
            Physics.SyncTransforms();
            bool result = BuildingSnap.CheckSupport(Vector3.zero, BuildingPieceType.Beam, true, 4f, new Collider[32]);
            Assert.IsTrue(result, "Beam should be supported by any piece.");
            Object.DestroyImmediate(wall);
        }

        [Test]
        public void CheckSupport_AngledWall_SupportedByAnyPiece()
        {
            var roof = CreatePieceWithSnaps(BuildingPieceType.Roof26, Vector3.zero);
            Physics.SyncTransforms();
            bool result = BuildingSnap.CheckSupport(Vector3.zero, BuildingPieceType.AngledWall, true, 4f, new Collider[32]);
            Assert.IsTrue(result, "AngledWall should be supported by any piece.");
            Object.DestroyImmediate(roof);
        }

        // ── BuildingRecipe ────────────────────────────────────────────────────

        [Test]
        public void BuildingRecipe_CanAfford_ReturnsTrueWhenSufficient()
        {
            DisableDevMode();
            try
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

                Assert.IsTrue(GameConstants.IsBuildingCostsEnabled(), "Building costs should be enabled for economy checks.");
                Assert.IsTrue(recipe.CanAfford(inv));

                Object.DestroyImmediate(go);
                Object.DestroyImmediate(recipe);
                Object.DestroyImmediate(wood);
            }
            finally { RestoreDevMode(); }
        }

        [Test]
        public void BuildingRecipe_CanAfford_ReturnsFalseWhenInsufficient()
        {
            DisableDevMode();
            try
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
            finally { RestoreDevMode(); }
        }

        [Test]
        public void BuildingRecipe_CanAfford_NullInventory_ReturnsFalse()
        {
            DisableDevMode();
            try
            {
                var recipe = ScriptableObject.CreateInstance<BuildingRecipe>();
                recipe.SetCost(new List<RecipeIngredient>());

                Assert.IsFalse(recipe.CanAfford(null));

                Object.DestroyImmediate(recipe);
            }
            finally { RestoreDevMode(); }
        }

        [Test]
        public void BuildingRecipe_ConsumeResources_RemovesItems()
        {
            DisableDevMode();
            try
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
            finally { RestoreDevMode(); }
        }

        // ── BuildingPreview Pitch ──────────────────────────────────────────

        [Test]
        public void BuildingPreview_RotatePitch_ClampedToMaxPitch()
        {
            var preview = new BuildingPreview();
            for (int i = 0; i < 10; i++)
                preview.RotatePitch(1f, 45f);
            Assert.AreEqual(BuildingPreview.MaxPitch, preview.PreviewPitch, 0.01f,
                "Pitch should be clamped to MaxPitch (90).");
        }

        [Test]
        public void BuildingPreview_RotatePitch_ClampedToNegativeMaxPitch()
        {
            var preview = new BuildingPreview();
            for (int i = 0; i < 10; i++)
                preview.RotatePitch(-1f, 45f);
            Assert.AreEqual(-BuildingPreview.MaxPitch, preview.PreviewPitch, 0.01f,
                "Pitch should be clamped to -MaxPitch (-90).");
        }

        [Test]
        public void BuildingPreview_RotatePitch_ZeroIsReachable()
        {
            var preview = new BuildingPreview();
            // Tilt up twice (0 → 45 → 90), then back down twice (90 → 45 → 0)
            preview.RotatePitch(1f, 45f);
            preview.RotatePitch(1f, 45f);
            preview.RotatePitch(-1f, 45f);
            preview.RotatePitch(-1f, 45f);
            Assert.AreEqual(0f, preview.PreviewPitch, 0.01f,
                "Pitch should return to exactly 0 after equal up/down steps.");
        }

        [Test]
        public void BuildingPreview_ResetPitch_SetsToZero()
        {
            var preview = new BuildingPreview();
            preview.RotatePitch(1f, 30f);
            Assert.AreNotEqual(0f, preview.PreviewPitch);
            preview.ResetPitch();
            Assert.AreEqual(0f, preview.PreviewPitch, 0.01f);
        }

        [Test]
        public void BuildingPreview_SnapPointLocals_InitiallyEmpty()
        {
            var preview = new BuildingPreview();
            Assert.IsNotNull(preview.SnapPointLocals);
            Assert.AreEqual(0, preview.SnapPointLocals.Count);
        }

        // ── Full Rotation Serialization ──────────────────────────────────────

        [Test]
        public void WorldSaveData_FullRotation_RoundTrips()
        {
            var data = new WorldSaveData();
            data.SavedAtUtc = "test";
            data.Buildings.Add(new BuildingSaveEntry
            {
                PieceType = (int)BuildingPieceType.Ramp,
                PosX = 5f,
                PosY = 1f,
                PosZ = 10f,
                RotX = 26f,
                RotY = 90f,
                RotZ = 0f,
                PlacedByClientId = 7
            });

            string json = JsonUtility.ToJson(data);
            var result = JsonUtility.FromJson<WorldSaveData>(json);
            Assert.AreEqual(1, result.Buildings.Count);
            Assert.AreEqual(26f, result.Buildings[0].RotX, 0.01f, "RotX should round-trip.");
            Assert.AreEqual(90f, result.Buildings[0].RotY, 0.01f, "RotY should round-trip.");
            Assert.AreEqual(0f, result.Buildings[0].RotZ, 0.01f, "RotZ should round-trip.");
        }

        [Test]
        public void WorldSaveData_BackwardCompat_DefaultRotXZ()
        {
            // Simulate old save data without RotX/RotZ fields
            string json = "{\"Buildings\":[{\"PieceType\":0,\"PosX\":0,\"PosY\":0,\"PosZ\":0,\"RotY\":45,\"PlacedByClientId\":0}],\"SavedAtUtc\":\"test\"}";
            var result = JsonUtility.FromJson<WorldSaveData>(json);
            Assert.AreEqual(0f, result.Buildings[0].RotX, 0.01f, "RotX should default to 0 for old saves.");
            Assert.AreEqual(0f, result.Buildings[0].RotZ, 0.01f, "RotZ should default to 0 for old saves.");
            Assert.AreEqual(45f, result.Buildings[0].RotY, 0.01f, "RotY should still work.");
        }

        // ── BuildingPieceType Enum ───────────────────────────────────────────

        [Test]
        public void BuildingPieceType_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)BuildingPieceType.Foundation);
            Assert.AreEqual(1, (int)BuildingPieceType.Wall);
            Assert.AreEqual(2, (int)BuildingPieceType.Floor);
            Assert.AreEqual(3, (int)BuildingPieceType.Ramp);
            Assert.AreEqual(4, (int)BuildingPieceType.Roof26);
            Assert.AreEqual(5, (int)BuildingPieceType.Stairs);
            Assert.AreEqual(6, (int)BuildingPieceType.Pole);
            Assert.AreEqual(7, (int)BuildingPieceType.Beam);
            Assert.AreEqual(8, (int)BuildingPieceType.AngledWall);
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
                RotX = 0f,
                RotY = 90f,
                RotZ = 0f,
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
                RotX = 0,
                RotY = 0,
                RotZ = 0,
                PlacedByClientId = 1
            });
            data.Buildings.Add(new BuildingSaveEntry
            {
                PieceType = (int)BuildingPieceType.Wall,
                PosX = 4,
                PosY = 0,
                PosZ = 0,
                RotX = 0,
                RotY = 180,
                RotZ = 0,
                PlacedByClientId = 2
            });

            string json = JsonUtility.ToJson(data);
            var result = JsonUtility.FromJson<WorldSaveData>(json);
            Assert.AreEqual(2, result.Buildings.Count);
            Assert.AreEqual((int)BuildingPieceType.Wall, result.Buildings[1].PieceType);
            Assert.AreEqual(180f, result.Buildings[1].RotY, 0.01f);
        }

        // ── New types serialization ──────────────────────────────────────────

        [Test]
        public void WorldSaveData_NewTypes_RoundTrip()
        {
            var data = new WorldSaveData();
            data.SavedAtUtc = "test";
            data.Buildings.Add(new BuildingSaveEntry
            {
                PieceType = (int)BuildingPieceType.Pole,
                PosX = 1f,
                PosY = 2f,
                PosZ = 3f,
                RotX = 0f,
                RotY = 0f,
                RotZ = 0f,
                PlacedByClientId = 1
            });
            data.Buildings.Add(new BuildingSaveEntry
            {
                PieceType = (int)BuildingPieceType.Beam,
                PosX = 4f,
                PosY = 5f,
                PosZ = 6f,
                RotX = 0f,
                RotY = 45f,
                RotZ = 0f,
                PlacedByClientId = 2
            });
            data.Buildings.Add(new BuildingSaveEntry
            {
                PieceType = (int)BuildingPieceType.AngledWall,
                PosX = 7f,
                PosY = 8f,
                PosZ = 9f,
                RotX = 26f,
                RotY = 90f,
                RotZ = 0f,
                PlacedByClientId = 3
            });

            string json = JsonUtility.ToJson(data);
            var result = JsonUtility.FromJson<WorldSaveData>(json);
            Assert.AreEqual(3, result.Buildings.Count);
            Assert.AreEqual((int)BuildingPieceType.Pole, result.Buildings[0].PieceType);
            Assert.AreEqual((int)BuildingPieceType.Beam, result.Buildings[1].PieceType);
            Assert.AreEqual((int)BuildingPieceType.AngledWall, result.Buildings[2].PieceType);
            Assert.AreEqual(26f, result.Buildings[2].RotX, 0.01f);
        }

        // ── BuildingMeshBuilder Tests ────────────────────────────────────────

        [Test]
        public void BuildRampMesh_HasValidGeometry()
        {
            var mesh = BuildingMeshBuilder.BuildRampMesh(4f, 3f, 4f);
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.vertexCount, 0, "Ramp mesh should have vertices.");
            Assert.Greater(mesh.triangles.Length, 0, "Ramp mesh should have triangles.");
            // 5 faces: bottom(2tri), slope(2tri), back(2tri), left(1tri), right(1tri) = 8 triangles × 3
            Assert.AreEqual(8 * 3, mesh.triangles.Length, "Ramp should have 8 triangles (24 indices).");
            Assert.AreEqual(18, mesh.vertexCount, "Ramp should have 18 vertices (duped per face).");
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void BuildRampMesh_BoundsMatchDimensions()
        {
            var mesh = BuildingMeshBuilder.BuildRampMesh(4f, 3f, 4f);
            // Bounds should span approximately width=4, height=3, depth=4
            Assert.AreEqual(4f, mesh.bounds.size.x, 0.01f, "Ramp width should be 4.");
            Assert.AreEqual(3f, mesh.bounds.size.y, 0.01f, "Ramp height should be 3.");
            Assert.AreEqual(4f, mesh.bounds.size.z, 0.01f, "Ramp depth should be 4.");
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void BuildGableRoofMesh_HasValidGeometry()
        {
            float ridgeH = Mathf.Tan(26f * Mathf.Deg2Rad) * 2f;
            var mesh = BuildingMeshBuilder.BuildGableRoofMesh(4f, 4f, ridgeH);
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.vertexCount, 0, "Roof mesh should have vertices.");
            Assert.Greater(mesh.triangles.Length, 0, "Roof mesh should have triangles.");
            // 5 faces: leftSlope(2tri), rightSlope(2tri), frontGable(1tri), backGable(1tri), bottom(2tri) = 8
            Assert.AreEqual(8 * 3, mesh.triangles.Length, "Roof should have 8 triangles (24 indices).");
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void BuildGableRoofMesh_RidgeHeightMatchesPeak()
        {
            float ridgeH = 1.5f;
            var mesh = BuildingMeshBuilder.BuildGableRoofMesh(4f, 4f, ridgeH);
            Assert.AreEqual(ridgeH, mesh.bounds.max.y, 0.01f, "Ridge should be at the specified height.");
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void BuildAngledWallMesh_HasValidGeometry()
        {
            float peakH = Mathf.Tan(26f * Mathf.Deg2Rad) * 2f;
            var mesh = BuildingMeshBuilder.BuildAngledWallMesh(4f, peakH, 0.3f);
            Assert.IsNotNull(mesh);
            Assert.Greater(mesh.vertexCount, 0, "AngledWall mesh should have vertices.");
            Assert.Greater(mesh.triangles.Length, 0, "AngledWall mesh should have triangles.");
            // 5 faces: front(1tri), back(1tri), bottom(2tri), left(2tri), right(2tri) = 8
            Assert.AreEqual(8 * 3, mesh.triangles.Length, "AngledWall should have 8 triangles (24 indices).");
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void BuildAngledWallMesh_PeakHeightMatchesSpec()
        {
            float peakH = Mathf.Tan(26f * Mathf.Deg2Rad) * 2f;
            var mesh = BuildingMeshBuilder.BuildAngledWallMesh(4f, peakH, 0.3f);
            Assert.AreEqual(peakH, mesh.bounds.max.y, 0.01f, "AngledWall peak should match tan(26)*2.");
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void BuildRampMesh_NormalsArePopulated()
        {
            var mesh = BuildingMeshBuilder.BuildRampMesh(4f, 3f, 4f);
            Assert.AreEqual(mesh.vertexCount, mesh.normals.Length, "Every vertex should have a normal.");
            // Verify normals are non-zero
            foreach (var n in mesh.normals)
                Assert.Greater(n.sqrMagnitude, 0.5f, "Normals should be unit-length (non-zero).");
            Object.DestroyImmediate(mesh);
        }

        // ── Collider Shape Tests ─────────────────────────────────────────────

        [Test]
        public void RampCollider_ShouldBeMeshCollider()
        {
            // Simulate what PrefabGenerator does with a Ramp
            var go = new GameObject("TestRamp");
            var mesh = BuildingMeshBuilder.BuildRampMesh(4f, 3f, 4f);
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex = true;

            Assert.IsNotNull(go.GetComponent<MeshCollider>(), "Ramp should have MeshCollider, not BoxCollider.");
            Assert.IsNull(go.GetComponent<BoxCollider>(), "Ramp should NOT have BoxCollider.");
            Assert.IsTrue(mc.convex, "Ramp MeshCollider should be convex for CharacterController.");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void StairsCollider_ShouldHavePerStepBoxColliders()
        {
            // Simulate stairs compound collider setup
            var go = new GameObject("TestStairs");
            const int stepCount = 12;
            float wallHeight = 3f;
            float stepH = wallHeight / stepCount;
            float depth = 4f;
            float stepD = depth / stepCount;
            float halfDepth = depth * 0.5f;

            for (int i = 0; i < stepCount; i++)
            {
                var stepCol = new GameObject($"StepCol{i}");
                stepCol.transform.SetParent(go.transform);
                float blockH = stepH * (i + 1);
                float centerY = blockH * 0.5f;
                float centerZ = -halfDepth + stepD * i + stepD * 0.5f;
                stepCol.transform.localPosition = new Vector3(0f, centerY, centerZ);
                var bc = stepCol.AddComponent<BoxCollider>();
                bc.center = Vector3.zero;
                bc.size = new Vector3(4f, blockH, stepD);
            }

            var allBoxColliders = go.GetComponentsInChildren<BoxCollider>();
            Assert.AreEqual(stepCount, allBoxColliders.Length, "Stairs should have 12 step box colliders.");

            // First step should be shortest, last should be tallest
            Assert.AreEqual(stepH, allBoxColliders[0].size.y, 0.01f, "First step height should be stepH.");
            Assert.AreEqual(wallHeight, allBoxColliders[stepCount - 1].size.y, 0.01f, "Last step height should be wallHeight.");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void PlayerCharacterController_ShouldHaveStepOffset()
        {
            // Verify the step offset is high enough to walk up steps
            const float stepH = 3f / 4f; // 0.75 per step
            const float expectedStepOffset = 0.4f;

            // The stepOffset should be at least half a step height to allow stepping up
            Assert.GreaterOrEqual(expectedStepOffset, stepH * 0.5f,
                "Player stepOffset should be at least half a step height to walk up stairs.");
        }

        // ── Nearest-Pair Snap Tests ──────────────────────────────────────────

        [Test]
        public void TryAdjacencySnap_PureNearestPair_SnapsEdgeToEdge()
        {
            // Placed floor at origin with edge midpoints and corners
            var placed = CreatePieceWithSnaps(BuildingPieceType.Floor, Vector3.zero,
                new Vector3(-2f, 0f, -2f), new Vector3(-2f, 0f, 2f),
                new Vector3(2f, 0f, -2f), new Vector3(2f, 0f, 2f),
                new Vector3(0f, 0f, -2f), new Vector3(0f, 0f, 2f),
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f));

            // Preview floor also has edge midpoints and corners
            var previewLocals = new List<Vector3>
            {
                new Vector3(-2f, 0f, -2f), new Vector3(-2f, 0f, 2f),
                new Vector3(2f, 0f, -2f), new Vector3(2f, 0f, 2f),
                new Vector3(0f, 0f, -2f), new Vector3(0f, 0f, 2f),
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f)
            };

            // Cursor near the +Z edge of the placed floor — should snap edge-to-edge
            // producing adjacent placement (new floor at z=+4)
            Vector3 cursorPos = new Vector3(0f, 0f, 4.5f);
            Quaternion rot = Quaternion.identity;
            var buffer = new Collider[32];
            Physics.SyncTransforms();

            bool snapped = BuildingSnap.TryAdjacencySnap(ref cursorPos, rot, previewLocals, 5f, buffer);

            Assert.IsTrue(snapped, "Should snap floor-to-floor.");
            // The placed piece has edge midpoint at (0,0,2). The preview has edge midpoint at (0,0,-2).
            // Expected: newPos = (0,0,2) - identity*(0,0,-2) = (0,0,4) — adjacent, not overlapping.
            Assert.AreEqual(4f, cursorPos.z, 0.5f, "Floor should snap adjacent (z=4), not overlapping (z=0).");
            Assert.AreEqual(0f, cursorPos.x, 0.5f, "X should stay centered.");

            Object.DestroyImmediate(placed);
        }

        [Test]
        public void TryAdjacencySnap_WallOnWall_StacksVertically()
        {
            // Place a wall at origin with 4 snap points
            var placed = CreatePieceWithSnaps(BuildingPieceType.Wall, Vector3.zero,
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f),
                new Vector3(-2f, 3f, 0f), new Vector3(2f, 3f, 0f));

            // Preview wall has identical snap points
            var previewLocals = new List<Vector3>
            {
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f),
                new Vector3(-2f, 3f, 0f), new Vector3(2f, 3f, 0f)
            };

            // Cursor above the wall — should snap wall bottom to wall top
            Vector3 cursorPos = new Vector3(0f, 4f, 0f);
            Quaternion rot = Quaternion.identity;
            var buffer = new Collider[32];
            Physics.SyncTransforms();

            bool snapped = BuildingSnap.TryAdjacencySnap(ref cursorPos, rot, previewLocals, 5f, buffer);

            Assert.IsTrue(snapped, "Should snap wall-on-wall.");
            // Preview bottom at (-2,0,0) closest to placed top at (-2,3,0)
            // newPos = (-2,3,0) - identity*(-2,0,0) = (0,3,0)
            Assert.AreEqual(3f, cursorPos.y, 0.5f, "Wall should stack at y=3, not y=0.");

            Object.DestroyImmediate(placed);
        }

        [Test]
        public void TryAdjacencySnap_ElevatedEdgeBuild_AllowsCornerSnapSelection()
        {
            // Regression guard for overhead/elevated building:
            // When the cursor position is offset (as when aiming into air in Valheim),
            // the nearest-pair algorithm must allow selecting a corner snap (not always an edge midpoint).

            // Placed "pole" with a top snap at (0,3,0)
            var placed = CreatePieceWithSnaps(BuildingPieceType.Pole, Vector3.zero,
                new Vector3(0f, 3f, 0f));

            float ridgeH = Mathf.Tan(26f * Mathf.Deg2Rad) * 2f;
            var roofLocals = new List<Vector3>
            {
                // corners
                new Vector3(-2f, 0f, -2f),
                new Vector3( 2f, 0f, -2f),
                new Vector3(-2f, 0f,  2f),
                new Vector3( 2f, 0f,  2f),
                // edge midpoints
                new Vector3(0f, 0f, -2f),
                new Vector3(0f, 0f,  2f),
                new Vector3(-2f, 0f, 0f),
                new Vector3( 2f, 0f, 0f),
                // ridge points
                new Vector3(0f, ridgeH, -2f),
                new Vector3(0f, ridgeH,  2f),
            };

            Vector3 poleTopWorld = placed.GetComponentsInChildren<SnapPointMarker>()[0].WorldPosition;
            Vector3 chosenCornerLocal = new Vector3(2f, 0f, 2f);

            // Pick a cursor position that puts THIS corner almost exactly on the pole top.
            Vector3 cursorPos = poleTopWorld - chosenCornerLocal + new Vector3(0.02f, 0f, -0.01f);
            var buffer = new Collider[32];
            Physics.SyncTransforms();

            bool snapped = BuildingSnap.TryAdjacencySnap(ref cursorPos, Quaternion.identity, roofLocals, 5f, buffer);

            Assert.IsTrue(snapped, "Should snap roof to pole top.");

            // The snapped position should essentially be poleTopWorld - chosenCornerLocal.
            Vector3 expected = poleTopWorld - chosenCornerLocal;
            Assert.AreEqual(expected.x, cursorPos.x, 0.2f);
            Assert.AreEqual(expected.y, cursorPos.y, 0.2f);
            Assert.AreEqual(expected.z, cursorPos.z, 0.2f);

            Object.DestroyImmediate(placed);
        }

        // ── Anti-Regression: Valheim-style Snap Invariants ───────────────────

        [Test]
        public void WallSnapPoints_AllAtCenterlineZ()
        {
            // Valheim walls are flat planes — all snap points must be at z=0.
            // Adding z-offset snap points causes misalignment during stacking.
            var go = CreatePieceWithSnaps(BuildingPieceType.Wall, Vector3.zero,
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f),
                new Vector3(-2f, 3f, 0f), new Vector3(2f, 3f, 0f));
            var markers = go.GetComponentsInChildren<SnapPointMarker>();
            foreach (var m in markers)
            {
                Assert.AreEqual(0f, m.transform.localPosition.z, 0.001f,
                    $"Wall snap point at {m.transform.localPosition} must have z=0 (centerline). " +
                    "Off-center z values cause stacking misalignment.");
            }
            Object.DestroyImmediate(go);
        }

        [Test]
        public void WallOnWall_NoZOffset_AfterStacking()
        {
            // Regression guard: stacking walls must produce z=0 alignment.
            var placed = CreatePieceWithSnaps(BuildingPieceType.Wall, Vector3.zero,
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f),
                new Vector3(-2f, 3f, 0f), new Vector3(2f, 3f, 0f));

            var previewLocals = new List<Vector3>
            {
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f),
                new Vector3(-2f, 3f, 0f), new Vector3(2f, 3f, 0f)
            };

            Vector3 cursorPos = new Vector3(0f, 4f, 0f);
            var buffer = new Collider[32];
            Physics.SyncTransforms();

            BuildingSnap.TryAdjacencySnap(ref cursorPos, Quaternion.identity, previewLocals, 5f, buffer);

            Assert.AreEqual(0f, cursorPos.z, 0.01f,
                "Stacked wall must have z=0. Any z-offset indicates regression from face-edge snap points.");

            Object.DestroyImmediate(placed);
        }

        [Test]
        public void FloorOnWall_SnapAligns_FloorEdgeMidToWallTop()
        {
            // When cursor is directly above a wall, the floor's edge midpoint (-2,0,0)
            // should match the wall's top corner (-2,3,0), centering the floor on the wall.
            // This IS correct Valheim behavior — floors center on walls from above.
            var wall = CreatePieceWithSnaps(BuildingPieceType.Wall, Vector3.zero,
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f),
                new Vector3(-2f, 3f, 0f), new Vector3(2f, 3f, 0f));

            var floorLocals = new List<Vector3>
            {
                new Vector3(-2f, 0f, -2f), new Vector3(-2f, 0f, 2f),
                new Vector3(2f, 0f, -2f), new Vector3(2f, 0f, 2f),
                new Vector3(0f, 0f, -2f), new Vector3(0f, 0f, 2f),
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f)
            };

            Vector3 cursorPos = new Vector3(0f, 3.5f, 0f);
            var buffer = new Collider[32];
            Physics.SyncTransforms();

            bool snapped = BuildingSnap.TryAdjacencySnap(ref cursorPos, Quaternion.identity, floorLocals, 5f, buffer);

            Assert.IsTrue(snapped, "Floor should snap to wall top.");
            Assert.AreEqual(3f, cursorPos.y, 0.5f, "Floor should be at wall height.");
            Assert.AreEqual(0f, cursorPos.z, 0.01f,
                "Floor z must equal wall z (both at z=0 centerline). " +
                "Non-zero z indicates face-edge offset regression.");

            Object.DestroyImmediate(wall);
        }

        [Test]
        public void SnapAlgorithm_IsTypeAgnostic_PureNearestPair()
        {
            // Any snap point should match any other snap point regardless of type.
            // This is the core Valheim invariant — no hard type filtering.
            // (Exception: Ridge points are restricted to Ridge-to-Ridge to prevent roof ridges snapping onto walls/floors.)
            var placed = CreatePieceWithSnaps(BuildingPieceType.Foundation, Vector3.zero,
                new Vector3(2f, 0.4f, 2f));  // Corner type

            var previewLocals = new List<Vector3>
            {
                new Vector3(-2f, 0f, 0f)  // Side type (different type)
            };

            Vector3 cursorPos = new Vector3(4.5f, 0.4f, 2f);
            var buffer = new Collider[32];
            Physics.SyncTransforms();

            bool snapped = BuildingSnap.TryAdjacencySnap(ref cursorPos, Quaternion.identity, previewLocals, 5f, buffer);

            Assert.IsTrue(snapped, "Snap must be type-agnostic (Corner matched with Side).");

            Object.DestroyImmediate(placed);
        }

        [Test]
        public void TryAdjacencySnap_PrefersAimedPiece_WhenProvided()
        {
            // Two candidate pieces are within range; prefer the one the player is aiming at.
            var a = CreatePieceWithSnaps(BuildingPieceType.Foundation, new Vector3(0f, 0f, 0f),
                new Vector3(2f, 0.4f, 0f));
            var b = CreatePieceWithSnaps(BuildingPieceType.Foundation, new Vector3(6f, 0f, 0f),
                new Vector3(2f, 0.4f, 0f));

            var preferred = b.GetComponent<BuildingPiece>();
            Assert.IsNotNull(preferred);

            var previewLocals = new List<Vector3> { new Vector3(-2f, 0f, 0f) };
            Vector3 cursorPos = new Vector3(5.1f, 0.4f, 0f);
            var buffer = new Collider[32];
            Physics.SyncTransforms();

            bool snapped = BuildingSnap.TryAdjacencySnap(
                ref cursorPos,
                Quaternion.identity,
                previewLocals,
                null,
                5f,
                buffer,
                preferred);

            Assert.IsTrue(snapped, "Should snap when a preferred piece is provided and within radius.");
            // Preferred piece is at x=6, its snap marker is at local (2,0.4,0) => world x=8.
            // newPos = 8 - (-2) = 10
            Assert.AreEqual(10f, cursorPos.x, 0.25f, "Snapped position should come from preferred piece.");

            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        [Test]
        public void TryAdjacencySnap_RidgeSnapPoints_DoNotSnapToNonRidge()
        {
            // Regression guard for the roof issue in screenshots:
            // Ridge snap points must not be eligible against non-ridge snap points.
            var placed = CreatePieceWithSnaps(BuildingPieceType.Foundation, Vector3.zero,
                new Vector3(0f, 0.4f, 0f));

            float ridgeH = Mathf.Tan(26f * Mathf.Deg2Rad) * 2f;
            var previewLocals = new List<Vector3>
            {
                new Vector3(0f, ridgeH, 2f), // Ridge
                new Vector3(0f, 0f, 2f),     // Side
            };
            var previewTypes = new List<SnapPointType>
            {
                SnapPointType.Ridge,
                SnapPointType.Side,
            };

            // Choose a cursor position that would make the ridge point match perfectly if it were allowed:
            // pos + ridgeLocal == placedWorld  =>  pos == placedWorld - ridgeLocal
            Vector3 placedWorld = placed.GetComponentsInChildren<SnapPointMarker>()[0].WorldPosition;
            Vector3 ridgeLocal = previewLocals[0];
            Vector3 ridgeBasedPos = placedWorld - ridgeLocal;
            Vector3 cursorPos = ridgeBasedPos;

            var buffer = new Collider[32];
            Physics.SyncTransforms();

            bool snapped = BuildingSnap.TryAdjacencySnap(
                ref cursorPos,
                Quaternion.identity,
                previewLocals,
                previewTypes,
                5f,
                buffer,
                null);

            Assert.IsTrue(snapped, "Should still snap using a non-ridge preview point.");
            Assert.Greater(Vector3.Distance(cursorPos, ridgeBasedPos), 0.25f,
                "Snap result must not be the ridge-based alignment; Ridge must not snap to non-ridge.");

            Object.DestroyImmediate(placed);
        }

        // ── New Piece Type Snap Point Count Tests ─────────────────────────────

        [Test]
        public void DoorFrame_ShouldHave6SnapPoints()
        {
            var go = CreatePieceWithSnaps(BuildingPieceType.DoorFrame, Vector3.zero,
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f),
                new Vector3(-2f, 3f, 0f), new Vector3(2f, 3f, 0f),
                new Vector3(-0.75f, 0f, 0f), new Vector3(0.75f, 0f, 0f));
            var markers = go.GetComponentsInChildren<SnapPointMarker>();
            Assert.AreEqual(6, markers.Length, "DoorFrame should have 6 snap points.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Window_ShouldHave4SnapPoints()
        {
            var go = CreatePieceWithSnaps(BuildingPieceType.Window, Vector3.zero,
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f),
                new Vector3(-2f, 3f, 0f), new Vector3(2f, 3f, 0f));
            var markers = go.GetComponentsInChildren<SnapPointMarker>();
            Assert.AreEqual(4, markers.Length, "Window should have 4 snap points.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void HalfWall_ShouldHave4SnapPoints()
        {
            var go = CreatePieceWithSnaps(BuildingPieceType.HalfWall, Vector3.zero,
                new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f),
                new Vector3(-2f, 1.5f, 0f), new Vector3(2f, 1.5f, 0f));
            var markers = go.GetComponentsInChildren<SnapPointMarker>();
            Assert.AreEqual(4, markers.Length, "HalfWall should have 4 snap points.");
            Object.DestroyImmediate(go);
        }

        // ── BuildingPieceType Enum — New Values ──────────────────────────────

        [Test]
        public void BuildingPieceType_HasExpectedNewValues()
        {
            Assert.AreEqual(9, (int)BuildingPieceType.DoorFrame);
            Assert.AreEqual(10, (int)BuildingPieceType.Window);
            Assert.AreEqual(11, (int)BuildingPieceType.HalfWall);
        }

        // ── SnapPointType Compatibility Tests (legacy, retained for coverage) ─

#pragma warning disable CS0618 // AreCompatible is obsolete
        [Test]
        public void SnapPointType_Generic_CompatibleWithAll()
        {
            Assert.IsTrue(SnapPointMarker.AreCompatible(SnapPointType.Generic, SnapPointType.Top));
            Assert.IsTrue(SnapPointMarker.AreCompatible(SnapPointType.Generic, SnapPointType.Bottom));
            Assert.IsTrue(SnapPointMarker.AreCompatible(SnapPointType.Generic, SnapPointType.Side));
            Assert.IsTrue(SnapPointMarker.AreCompatible(SnapPointType.Generic, SnapPointType.Ridge));
            Assert.IsTrue(SnapPointMarker.AreCompatible(SnapPointType.Generic, SnapPointType.Corner));
            Assert.IsTrue(SnapPointMarker.AreCompatible(SnapPointType.Generic, SnapPointType.Generic));
        }

        [Test]
        public void SnapPointType_TopBottom_Compatible()
        {
            Assert.IsTrue(SnapPointMarker.AreCompatible(SnapPointType.Top, SnapPointType.Bottom));
            Assert.IsTrue(SnapPointMarker.AreCompatible(SnapPointType.Bottom, SnapPointType.Top));
        }

        [Test]
        public void SnapPointType_SameSide_Compatible()
        {
            Assert.IsTrue(SnapPointMarker.AreCompatible(SnapPointType.Side, SnapPointType.Side));
            Assert.IsTrue(SnapPointMarker.AreCompatible(SnapPointType.Corner, SnapPointType.Corner));
            Assert.IsTrue(SnapPointMarker.AreCompatible(SnapPointType.Ridge, SnapPointType.Ridge));
        }

        [Test]
        public void SnapPointType_Incompatible_ReturnsFalse()
        {
            Assert.IsFalse(SnapPointMarker.AreCompatible(SnapPointType.Top, SnapPointType.Top));
            Assert.IsFalse(SnapPointMarker.AreCompatible(SnapPointType.Bottom, SnapPointType.Bottom));
            Assert.IsFalse(SnapPointMarker.AreCompatible(SnapPointType.Side, SnapPointType.Ridge));
            Assert.IsFalse(SnapPointMarker.AreCompatible(SnapPointType.Corner, SnapPointType.Side));
            Assert.IsFalse(SnapPointMarker.AreCompatible(SnapPointType.Top, SnapPointType.Side));
        }
#pragma warning restore CS0618

        // ── CheckSupport — New Piece Types ───────────────────────────────────

        [Test]
        public void CheckSupport_DoorFrame_SupportedByFoundation()
        {
            var foundation = CreatePieceWithSnaps(BuildingPieceType.Foundation, Vector3.zero);
            Physics.SyncTransforms();
            bool result = BuildingSnap.CheckSupport(Vector3.zero, BuildingPieceType.DoorFrame, true, 4f, new Collider[32]);
            Assert.IsTrue(result, "DoorFrame should be supported by Foundation.");
            Object.DestroyImmediate(foundation);
        }

        [Test]
        public void CheckSupport_Window_SupportedByWall()
        {
            var wall = CreatePieceWithSnaps(BuildingPieceType.Wall, Vector3.zero);
            Physics.SyncTransforms();
            bool result = BuildingSnap.CheckSupport(Vector3.zero, BuildingPieceType.Window, true, 4f, new Collider[32]);
            Assert.IsTrue(result, "Window should be supported by Wall.");
            Object.DestroyImmediate(wall);
        }

        [Test]
        public void CheckSupport_HalfWall_SupportedByFloor()
        {
            var floor = CreatePieceWithSnaps(BuildingPieceType.Floor, Vector3.zero);
            Physics.SyncTransforms();
            bool result = BuildingSnap.CheckSupport(Vector3.zero, BuildingPieceType.HalfWall, true, 4f, new Collider[32]);
            Assert.IsTrue(result, "HalfWall should be supported by Floor.");
            Object.DestroyImmediate(floor);
        }

        [Test]
        public void CheckSupport_DoorFrame_NotSupportedAlone()
        {
            Assert.IsFalse(BuildingSnap.CheckSupport(Vector3.zero, BuildingPieceType.DoorFrame, false, 4f, new Collider[32]),
                "DoorFrame should not be supported without snap.");
        }

        // ── CheckNoOverlap Tests ─────────────────────────────────────────────

        [Test]
        public void CheckNoOverlap_EmptySpace_ReturnsTrue()
        {
            var buffer = new Collider[32];
            bool result = BuildingSnap.CheckNoOverlap(
                new Vector3(100f, 100f, 100f), Quaternion.identity, Vector3.one, buffer);
            Assert.IsTrue(result, "Empty space should have no overlap.");
        }

        [Test]
        public void CheckNoOverlap_OverlappingPiece_ReturnsFalse()
        {
            var placed = CreatePieceWithSnaps(BuildingPieceType.Foundation, Vector3.zero);
            Physics.SyncTransforms();
            var buffer = new Collider[32];
            bool result = BuildingSnap.CheckNoOverlap(
                Vector3.zero, Quaternion.identity, new Vector3(2f, 1.5f, 2f), buffer);
            Assert.IsFalse(result, "Overlapping foundation should be detected.");
            Object.DestroyImmediate(placed);
        }

        // ── CheckPlacementRestrictions Tests ─────────────────────────────────

        [Test]
        public void CheckPlacementRestrictions_NullPiece_ReturnsTrue()
        {
            Assert.IsTrue(BuildingSnap.CheckPlacementRestrictions(Vector3.zero, null, 0f));
        }

        [Test]
        public void CheckPlacementRestrictions_OnlyOnFlat_SteepDenied()
        {
            var go = new GameObject("TestPiece");
            var piece = go.AddComponent<BuildingPiece>();
            // Set _onlyOnFlat via reflection
            var field = typeof(BuildingPiece).GetField("_onlyOnFlat",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(piece, true);

            bool result = BuildingSnap.CheckPlacementRestrictions(Vector3.zero, piece, 30f);
            Assert.IsFalse(result, "OnlyOnFlat should deny steep surfaces.");

            Object.DestroyImmediate(go);
        }

        // ── StructuralMaterial Tests ─────────────────────────────────────────

        [Test]
        public void StructuralMaterial_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)StructuralMaterial.Wood);
            Assert.AreEqual(1, (int)StructuralMaterial.Stone);
            Assert.AreEqual(2, (int)StructuralMaterial.Iron);
            Assert.AreEqual(3, (int)StructuralMaterial.HardWood);
        }

        // ── ComfortGroup Tests ───────────────────────────────────────────────

        [Test]
        public void ComfortGroup_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)ComfortGroup.None);
            Assert.AreEqual(1, (int)ComfortGroup.Fire);
            Assert.AreEqual(2, (int)ComfortGroup.Bed);
            Assert.AreEqual(3, (int)ComfortGroup.Banner);
            Assert.AreEqual(4, (int)ComfortGroup.Table);
            Assert.AreEqual(5, (int)ComfortGroup.Chair);
            Assert.AreEqual(6, (int)ComfortGroup.Rug);
            Assert.AreEqual(7, (int)ComfortGroup.Shelf);
        }

        // ── StructuralIntegrity Tests ────────────────────────────────────────

        [Test]
        public void StructuralIntegrity_InitialSupportValue_IsValid()
        {
            var go = new GameObject("TestPiece");
            go.AddComponent<BoxCollider>();
            var piece = go.AddComponent<BuildingPiece>();
            var integrity = go.AddComponent<StructuralIntegrity>();
            // SupportValue should be initialized (default 1.0)
            Assert.GreaterOrEqual(integrity.SupportValue, 0f, "Support value should not be negative.");
            Assert.LessOrEqual(integrity.SupportValue, 1f, "Support value should not exceed 1.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void StructuralIntegrity_GetSupportColor_ReturnsValidColors()
        {
            var go = new GameObject("TestPiece");
            go.AddComponent<BoxCollider>();
            go.AddComponent<BuildingPiece>();
            var integrity = go.AddComponent<StructuralIntegrity>();

            Color color = integrity.GetSupportColor();
            // With default support 1.0, should return blue (excellent)
            Assert.AreEqual(Color.blue, color, "Full support should return blue.");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void StructuralIntegrity_StaticRegistry_RegistersOnEnable()
        {
            int countBefore = StructuralIntegrity.Instances.Count;
            var go = new GameObject("TestPiece");
            go.AddComponent<BoxCollider>();
            go.AddComponent<BuildingPiece>();
            var integrity = go.AddComponent<StructuralIntegrity>();

            // OnEnable is not called in EditMode — invoke manually
            var onEnable = typeof(StructuralIntegrity).GetMethod("OnEnable",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            onEnable.Invoke(integrity, null);

            Assert.AreEqual(countBefore + 1, StructuralIntegrity.Instances.Count,
                "Instance should be registered on enable.");

            Object.DestroyImmediate(go);
        }

        // ── SnapPointMarker SnapType property Tests ──────────────────────────

        [Test]
        public void SnapPointMarker_SnapType_DefaultsToGeneric()
        {
            var go = new GameObject("Snap");
            var marker = go.AddComponent<SnapPointMarker>();
            Assert.AreEqual(SnapPointType.Generic, marker.SnapType);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SnapPointMarker_SnapType_RoundTrips()
        {
            var go = new GameObject("Snap");
            var marker = go.AddComponent<SnapPointMarker>();
            marker.SnapType = SnapPointType.Ridge;
            Assert.AreEqual(SnapPointType.Ridge, marker.SnapType);
            Object.DestroyImmediate(go);
        }

        // ── WorldSaveData — New Types Serialization ──────────────────────────

        [Test]
        public void WorldSaveData_NewPieceTypes_RoundTrip()
        {
            var data = new WorldSaveData();
            data.SavedAtUtc = "test";
            data.Buildings.Add(new BuildingSaveEntry
            {
                PieceType = (int)BuildingPieceType.DoorFrame,
                PosX = 1f,
                PosY = 0f,
                PosZ = 0f,
                RotX = 0f,
                RotY = 0f,
                RotZ = 0f,
                PlacedByClientId = 1
            });
            data.Buildings.Add(new BuildingSaveEntry
            {
                PieceType = (int)BuildingPieceType.Window,
                PosX = 2f,
                PosY = 0f,
                PosZ = 0f,
                RotX = 0f,
                RotY = 90f,
                RotZ = 0f,
                PlacedByClientId = 2
            });
            data.Buildings.Add(new BuildingSaveEntry
            {
                PieceType = (int)BuildingPieceType.HalfWall,
                PosX = 3f,
                PosY = 0f,
                PosZ = 0f,
                RotX = 0f,
                RotY = 0f,
                RotZ = 0f,
                PlacedByClientId = 3
            });

            string json = JsonUtility.ToJson(data);
            var result = JsonUtility.FromJson<WorldSaveData>(json);
            Assert.AreEqual(3, result.Buildings.Count);
            Assert.AreEqual((int)BuildingPieceType.DoorFrame, result.Buildings[0].PieceType);
            Assert.AreEqual((int)BuildingPieceType.Window, result.Buildings[1].PieceType);
            Assert.AreEqual((int)BuildingPieceType.HalfWall, result.Buildings[2].PieceType);
        }
    }
}
