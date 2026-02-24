using UnityEditor;
using UnityEngine;

namespace ByteWar.Editor
{
    /// <summary>
    /// Ensures the project identity (company/product) remains stable.
    /// This matters for deterministic paths like Player.log under LocalLow.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectIdentityEnforcer
    {
        private const string ExpectedCompany = "BytedotGit";
        private const string ExpectedProduct = "ByteWar";

        static ProjectIdentityEnforcer()
        {
            try
            {
                string company = PlayerSettings.companyName;
                string product = PlayerSettings.productName;

                if (company == ExpectedCompany && product == ExpectedProduct)
                    return;

                PlayerSettings.companyName = ExpectedCompany;
                PlayerSettings.productName = ExpectedProduct;
                Debug.Log($"[ProjectIdentityEnforcer] Set PlayerSettings company/product to '{ExpectedCompany}/{ExpectedProduct}' (was '{company}/{product}').");

                // In case the settings asset needs persistence.
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ProjectIdentityEnforcer] Failed to enforce identity: {ex.Message}");
            }
        }
    }
}
