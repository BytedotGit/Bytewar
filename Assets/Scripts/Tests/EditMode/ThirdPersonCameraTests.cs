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

        [Test]
        public void MouseButtons_DefaultToFalse()
        {
            // In EditMode (no mouse device), both buttons should default to false
            var camGo = new GameObject("Test_ThirdPersonCamera");
            try
            {
                var camera = camGo.AddComponent<ThirdPersonCamera>();
                Assert.IsFalse(camera.IsLeftMouseHeld, "IsLeftMouseHeld should default to false.");
                Assert.IsFalse(camera.IsRightMouseHeld, "IsRightMouseHeld should default to false.");
                Assert.IsFalse(camera.IsLeftMouseDragging, "IsLeftMouseDragging should default to false.");
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void AutoTest_LmbClick_DoesNotOrbitYaw()
        {
            var camGo = new GameObject("Test_ThirdPersonCamera");
            var target = new GameObject("CameraTarget");

            try
            {
                var camera = camGo.AddComponent<ThirdPersonCamera>();
                camera.SetTarget(target.transform, pivotHeight: 0f);

                camera.AutoTest_SetOrbitAngles(0f, 0f, immediate: true);
                camera.AutoTest_SetMouseState(leftHeld: true, rightHeld: false, leftDragging: false);
                camera.AutoTest_OrbitTick(new Vector2(50f, 0f));

                Assert.That(camera.CameraYaw, Is.EqualTo(0f).Within(0.01f), "LMB click (no drag) should not orbit camera.");
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void AutoTest_LmbDrag_DoesOrbitYaw()
        {
            var camGo = new GameObject("Test_ThirdPersonCamera");
            var target = new GameObject("CameraTarget");

            try
            {
                var camera = camGo.AddComponent<ThirdPersonCamera>();
                camera.SetTarget(target.transform, pivotHeight: 0f);

                camera.AutoTest_SetOrbitAngles(0f, 0f, immediate: true);
                camera.AutoTest_SetMouseState(leftHeld: true, rightHeld: false, leftDragging: true);
                camera.AutoTest_OrbitTick(new Vector2(50f, 0f));

                Assert.That(camera.CameraYaw, Is.GreaterThan(0.1f), "LMB drag should orbit camera.");
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void AutoTest_KeyboardTurnFollow_AlignsYawToTargetWhenNotOrbiting()
        {
            var camGo = new GameObject("Test_ThirdPersonCamera");
            var targetRoot = new GameObject("TargetRoot");
            var target = new GameObject("CameraTarget");

            try
            {
                target.transform.SetParent(targetRoot.transform);

                var camera = camGo.AddComponent<ThirdPersonCamera>();
                camera.SetTarget(target.transform, pivotHeight: 0f);
                camera.AutoTest_SetOrbitAngles(0f, 0f, immediate: true);
                camera.AutoTest_SetMouseState(leftHeld: false, rightHeld: false, leftDragging: false);

                targetRoot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                camera.AutoTest_ApplyKeyboardTurnFollowTick(1f);

                Assert.That(camera.CameraYaw, Is.EqualTo(90f).Within(0.1f), "Camera yaw should follow target yaw when not orbiting.");
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void AutoTest_FirstPerson_ExpandsPitchClamp_ToLookStraightUpDown()
        {
            var camGo = new GameObject("Test_ThirdPersonCamera");
            var target = new GameObject("CameraTarget");

            try
            {
                var camera = camGo.AddComponent<ThirdPersonCamera>();
                camera.SetTarget(target.transform, pivotHeight: 0f);

                // First-person (distance=0): allow near ±90°.
                camera.AutoTest_SetTargetDistance(0f, immediate: true);
                camera.AutoTest_SetOrbitAngles(0f, 999f, immediate: true);
                camera.AutoTest_ClampPitchOnce();
                Assert.That(camera.AutoTest_GetPitch(), Is.EqualTo(89.9f).Within(0.05f));

                camera.AutoTest_SetOrbitAngles(0f, -999f, immediate: true);
                camera.AutoTest_ClampPitchOnce();
                Assert.That(camera.AutoTest_GetPitch(), Is.EqualTo(-89.9f).Within(0.05f));

                // Third-person (distance=8): keep configured clamp (maxPitch=85 by default).
                camera.AutoTest_SetTargetDistance(8f, immediate: true);
                camera.AutoTest_SetOrbitAngles(0f, 999f, immediate: true);
                camera.AutoTest_ClampPitchOnce();
                Assert.That(camera.AutoTest_GetPitch(), Is.EqualTo(85f).Within(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(target);
            }
        }
    }
}
