using System.Collections;
using NUnit.Framework;
using ByteWar.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace ByteWar.Tests.PlayMode
{
    public class ThirdPersonCameraFirstPersonTests
    {
        [UnityTest]
        public IEnumerator FirstPersonMode_PlacesCameraAtPivot_AndHidesRenderers()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();

            var playerRoot = new GameObject("PlayerRoot");
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var target = new GameObject("CameraTarget");

            try
            {
                visual.transform.SetParent(playerRoot.transform);
                target.transform.SetParent(playerRoot.transform);
                target.transform.position = new Vector3(0f, 1.5f, 0f);

                var tpc = camGo.AddComponent<ThirdPersonCamera>();
                tpc.SetTarget(target.transform, pivotHeight: 0.20f);

                var r = visual.GetComponent<Renderer>();
                Assert.IsNotNull(r);

                // Force first-person.
                tpc.AutoTest_SetTargetDistance(0f, immediate: true);
                tpc.AutoTest_TickCameraTransformOnce();
                yield return null;

                Vector3 expectedPivot = target.transform.position + Vector3.up * tpc.PivotHeight;
                Assert.That(Vector3.Distance(cam.transform.position, expectedPivot), Is.LessThan(0.05f), "Camera should be placed at pivot in first-person.");
                Assert.IsFalse(r.enabled, "Player renderer should be hidden in first-person.");
            }
            finally
            {
                Object.Destroy(camGo);
                Object.Destroy(playerRoot);
            }
        }
    }
}
