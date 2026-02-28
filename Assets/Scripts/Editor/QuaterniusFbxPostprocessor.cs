using System;
using UnityEditor;
using UnityEngine;

namespace ByteWar.Editor
{
    /// <summary>
    /// Enforces deterministic import settings for Quaternius premade FBX assets.
    /// This keeps orientation/material behavior consistent between editor and batch builds.
    /// </summary>
    public sealed class QuaterniusFbxPostprocessor : AssetPostprocessor
    {
        private const string UltimateNatureRoot = "Assets/Art/Premade/Quaternius/UltimateNature/FBX/";
        private const string UltimateCropsRoot = "Assets/Art/Premade/Quaternius/UltimateCrops/FBX/";
        private const string StylizedNatureMegaKitTreesRoot = "Assets/Art/Premade/Quaternius/Environment/Vegetation/";

        private void OnPreprocessModel()
        {
            if (!IsTargetPremadePath(assetPath))
                return;

            var importer = (ModelImporter)assetImporter;

            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;

            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialName = ModelImporterMaterialName.BasedOnModelNameAndMaterialName;
            importer.materialSearch = ModelImporterMaterialSearch.Everywhere;

            importer.addCollider = false;

            Debug.Log($"[QuaterniusFbxPostprocessor] Applied import settings: {assetPath}");
        }

        internal static bool IsTargetPremadePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return path.StartsWith(UltimateNatureRoot, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(UltimateCropsRoot, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(StylizedNatureMegaKitTreesRoot, StringComparison.OrdinalIgnoreCase);
        }
    }
}
