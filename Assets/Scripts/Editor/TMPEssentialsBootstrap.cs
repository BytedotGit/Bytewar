using UnityEditor;
using UnityEngine;
using TMPro;

namespace ByteWar.Editor
{
    /// <summary>
    /// Ensures TMP essentials are imported so TMP never tries to open its importer
    /// window in batch/headless mode (which causes "No graphic device" errors in PlayMode tests).
    /// Runs automatically on domain reload via [InitializeOnLoad].
    /// </summary>
    [InitializeOnLoad]
    public static class TMPEssentialsBootstrap
    {
        static TMPEssentialsBootstrap()
        {
            // Check if TMP Settings asset exists
            var settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings != null) return;

            Debug.Log("[TMPEssentialsBootstrap] TMP Settings not found. Importing TMP Essentials...");

            // Delay to avoid import during domain reload
            EditorApplication.delayCall += ImportEssentials;
        }

        private static void ImportEssentials()
        {
            // Re-check in case another process imported them
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) return;

            TMP_PackageResourceImporter.ImportResources(true, false, false);
            Debug.Log("[TMPEssentialsBootstrap] TMP Essentials imported successfully.");
        }
    }
}
