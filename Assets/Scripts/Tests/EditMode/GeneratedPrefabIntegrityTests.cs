using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ByteWar.Core;

namespace ByteWar.Tests.EditMode
{
    public class GeneratedPrefabIntegrityTests
    {
        [Test]
        public void Resources_HasGeneratedPlayerAnimatorController()
        {
            var controller = Resources.Load<RuntimeAnimatorController>("Generated/PlayerAnimatorController");
            Assert.IsNotNull(controller, "Resources/Generated/PlayerAnimatorController.controller is missing. Run Generate Animator Controller.");
        }

        [Test]
        public void NetworkPlayerPrefab_HasAnimatorController()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedPrefabs/NetworkPlayer.prefab");
            Assert.IsNotNull(prefab, "NetworkPlayer.prefab not found. Run Generate All.");

            var instance = Object.Instantiate(prefab);
            try
            {
                var animator = instance.GetComponent<Animator>();
                Assert.IsNotNull(animator, "Animator missing on NetworkPlayer prefab.");
                Assert.IsNotNull(animator.runtimeAnimatorController, "Animator controller is null on NetworkPlayer prefab (T-pose risk). Run Generate Animator Controller.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

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

        [Test]
        public void NetworkPlayerPrefab_HasFootstepController()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedPrefabs/NetworkPlayer.prefab");
            Assert.IsNotNull(prefab, "NetworkPlayer.prefab not found. Run Generate All.");

            var instance = Object.Instantiate(prefab);
            try
            {
                var fc = instance.GetComponent<FootstepController>();
                Assert.IsNotNull(fc, "FootstepController missing on NetworkPlayer prefab. Run Generate Prefabs.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void NetworkPlayerPrefab_HasAudioSource()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedPrefabs/NetworkPlayer.prefab");
            Assert.IsNotNull(prefab, "NetworkPlayer.prefab not found. Run Generate All.");

            var instance = Object.Instantiate(prefab);
            try
            {
                var src = instance.GetComponent<AudioSource>();
                Assert.IsNotNull(src, "AudioSource missing on NetworkPlayer prefab. Run Generate Prefabs.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
