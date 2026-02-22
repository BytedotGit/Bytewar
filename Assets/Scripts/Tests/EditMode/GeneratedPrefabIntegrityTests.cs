using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalRPG.Tests.EditMode
{
    public class GeneratedPrefabIntegrityTests
    {
        [Test]
        public void NetworkPlayerPrefab_HasCameraTarget()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedPrefabs/NetworkPlayer.prefab");
            Assert.IsNotNull(prefab, "NetworkPlayer.prefab not found. Run Generate All.");

            var instance = Object.Instantiate(prefab);
            try
            {
                var target = instance.transform.Find("CameraTarget");
                Assert.IsNotNull(target, "CameraTarget child missing on NetworkPlayer prefab.");
                Assert.Greater(target.localPosition.y, 0.5f, "CameraTarget local Y should be above ground.");
                Assert.Less(target.localPosition.y, 2.5f, "CameraTarget local Y is unreasonably high.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void NetworkPlayerPrefab_CharacterControllerCenter_IsHalfHeight()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedPrefabs/NetworkPlayer.prefab");
            Assert.IsNotNull(prefab, "NetworkPlayer.prefab not found. Run Generate All.");

            var instance = Object.Instantiate(prefab);
            try
            {
                var cc = instance.GetComponent<CharacterController>();
                Assert.IsNotNull(cc, "CharacterController missing on NetworkPlayer prefab.");
                Assert.Greater(cc.height, 0.5f, "CharacterController.height should be set.");
                Assert.Greater(cc.center.y, 0.1f, "CharacterController.center.y must not be 0 (causes hovering).");

                float expected = cc.height * 0.5f;
                Assert.AreEqual(expected, cc.center.y, 0.001f, "CharacterController.center.y should be height/2.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
