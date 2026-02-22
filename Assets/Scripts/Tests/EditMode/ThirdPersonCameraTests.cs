using NUnit.Framework;
using ByteWar.Core;
using UnityEngine;

namespace ByteWar.Tests.EditMode
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

        [Test]
        public void AutoTest_FirstPerson_DisablesPlayerRenderers_WhenZoomedIn()
        {
            var camGo = new GameObject("Test_ThirdPersonCamera");
            var playerRoot = new GameObject("PlayerRoot");
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var target = new GameObject("CameraTarget");

            try
            {
                visual.name = "Visual";
                visual.transform.SetParent(playerRoot.transform);
                target.transform.SetParent(playerRoot.transform);
                target.transform.localPosition = Vector3.up * 1.5f;

                var renderer = visual.GetComponent<Renderer>();
                Assert.IsNotNull(renderer);
                Assert.IsTrue(renderer.enabled, "Renderer should start enabled.");

                var camera = camGo.AddComponent<ThirdPersonCamera>();
                camera.SetTarget(target.transform, pivotHeight: 0f);

                camera.AutoTest_SetTargetDistance(0f, immediate: true);
                camera.AutoTest_TickCameraTransformOnce();

                Assert.IsFalse(renderer.enabled, "Renderer should be disabled in first-person.");
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(playerRoot);
            }
        }

        [Test]
        public void AutoTest_FirstPerson_UsesHysteresis_ToAvoidFlicker()
        {
            var camGo = new GameObject("Test_ThirdPersonCamera");
            var playerRoot = new GameObject("PlayerRoot");
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var target = new GameObject("CameraTarget");

            try
            {
                visual.transform.SetParent(playerRoot.transform);
                target.transform.SetParent(playerRoot.transform);

                var renderer = visual.GetComponent<Renderer>();
                Assert.IsNotNull(renderer);

                var camera = camGo.AddComponent<ThirdPersonCamera>();
                camera.SetTarget(target.transform, pivotHeight: 0f);

                // Enter first-person.
                camera.AutoTest_SetTargetDistance(0f, immediate: true);
                camera.AutoTest_TickCameraTransformOnce();
                Assert.IsFalse(renderer.enabled, "Renderer should be disabled after entering first-person.");

                // Move within hysteresis band: should remain first-person.
                camera.AutoTest_SetTargetDistance(0.60f, immediate: true);
                camera.AutoTest_TickCameraTransformOnce();
                Assert.IsFalse(renderer.enabled, "Renderer should remain disabled within hysteresis band.");

                // Exceed exit threshold: should leave first-person and show renderers.
                camera.AutoTest_SetTargetDistance(0.70f, immediate: true);
                camera.AutoTest_TickCameraTransformOnce();
                Assert.IsTrue(renderer.enabled, "Renderer should be enabled after exiting first-person.");
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(playerRoot);
            }
        }
    }
}
