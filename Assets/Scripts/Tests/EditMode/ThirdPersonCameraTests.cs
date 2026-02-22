using NUnit.Framework;
using SurvivalRPG.Core;
using UnityEngine;

namespace SurvivalRPG.Tests.EditMode
{
    public class ThirdPersonCameraTests
    {
        [Test]
        public void SetTarget_UpdatesCameraYawToTargetYaw()
        {
            var camGo = new GameObject("Test_ThirdPersonCamera");
            var targetRoot = new GameObject("TargetRoot");
            var target = new GameObject("CameraTarget");

            try
            {
                target.transform.SetParent(targetRoot.transform);
                targetRoot.transform.rotation = Quaternion.Euler(0f, 123f, 0f);

                var camera = camGo.AddComponent<ThirdPersonCamera>();
                camera.SetTarget(target.transform);

                Assert.That(camera.CameraYaw, Is.EqualTo(123f).Within(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(targetRoot);
            }
        }
    }
}
