using NUnit.Framework;
using ByteWar.Core;
using ByteWar.Networking;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using UnityEngine;

namespace ByteWar.Tests.EditMode
{
    public class AutoTestNetworkingDeterminismTests
    {
        [Test]
        public void CustomNetworkManagerHUD_ShouldAutoHost_False_WhenAutoTestArgPresent()
        {
            string[] args = { "-autoTest" };
            bool should = CustomNetworkManagerHUD.ShouldAutoHost(
                isEditor: false,
                isServer: false,
                isClient: false,
                args: args);
            Assert.IsFalse(should);
        }

        [Test]
        public void CustomNetworkManagerHUD_ShouldAutoHost_False_WhenNoAutoHostArgPresent()
        {
            string[] args = { "-noAutoHost" };
            bool should = CustomNetworkManagerHUD.ShouldAutoHost(
                isEditor: false,
                isServer: false,
                isClient: false,
                args: args);
            Assert.IsFalse(should);
        }

        [Test]
        public void CustomNetworkManagerHUD_ShouldAutoHost_True_WhenStandaloneAndNoBlockArgs()
        {
            string[] args = { "-someOtherArg" };
            bool should = CustomNetworkManagerHUD.ShouldAutoHost(
                isEditor: false,
                isServer: false,
                isClient: false,
                args: args);
            Assert.IsTrue(should);
        }

        [Test]
        public void AutoTester_HasArg_IsCaseInsensitive()
        {
            string[] args = { "-AUTOTEST" };
            Assert.IsTrue(AutoTester.HasArg(args, AutoTester.AutoTestArg));
        }

        [Test]
        public void AutoTester_ChooseAutoTestPort_IsDeterministicAndInRange()
        {
            const int seed = 12345;
            int p0 = AutoTester.ChooseAutoTestPort(attemptIndex: 0, seed: seed);
            int p1 = AutoTester.ChooseAutoTestPort(attemptIndex: 1, seed: seed);
            int p0b = AutoTester.ChooseAutoTestPort(attemptIndex: 0, seed: seed);

            Assert.AreEqual(p0, p0b);
            Assert.AreNotEqual(p0, p1);

            Assert.That(p0, Is.InRange(45000, 54999));
            Assert.That(p1, Is.InRange(45000, 54999));
        }

        [Test]
        public void AutoTester_IsUdpPortAvailable_ReturnsFalseWhenPortIsBound()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.ExclusiveAddressUse = true;
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            int port = ((IPEndPoint)socket.LocalEndPoint).Port;
            Assert.That(port, Is.InRange(1, 65535));

            Assert.IsFalse(AutoTester.IsUdpPortAvailable(port), "Port should not be available while a UDP socket is bound to it.");
        }

        [Test]
        public void AutoTester_ComputeHostRetryDelaySeconds_IsDeterministic()
        {
            Assert.AreEqual(0.10f, AutoTester.ComputeHostRetryDelaySeconds(0), 0.0001f);
            Assert.AreEqual(0.25f, AutoTester.ComputeHostRetryDelaySeconds(1), 0.0001f);
            Assert.AreEqual(0.50f, AutoTester.ComputeHostRetryDelaySeconds(2), 0.0001f);
            Assert.AreEqual(0.50f, AutoTester.ComputeHostRetryDelaySeconds(999), 0.0001f);
        }

        [Test]
        public void AutoTester_ParseScenarioArgs_CollectsRequestedScenarioNames_CaseInsensitive()
        {
            string[] args = { "-autoTest", "-autoTestScenario", "Core", "-AUTOTESTSCENARIO", "Destruction" };

            var parsed = AutoTester.ParseScenarioArgs(args);

            Assert.IsTrue(parsed.Contains("core"));
            Assert.IsTrue(parsed.Contains("DESTRUCTION"));
            Assert.AreEqual(2, parsed.Count);
        }

        [Test]
        public void AutoTester_BuildScenarioList_ReturnsOnlyRequestedDestructionScenario()
        {
            string[] args = { "-autoTest", "-autoTestScenario", "Destruction" };

            var scenarios = AutoTester.BuildScenarioList(args);

            Assert.AreEqual(1, scenarios.Length);
            Assert.AreEqual("Destruction", scenarios[0].Name);
        }

        [Test]
        public void AutoTester_BuildScenarioList_DefaultIncludesDestructionScenario()
        {
            string[] args = { "-autoTest" };

            var scenarios = AutoTester.BuildScenarioList(args);

            Assert.IsTrue(scenarios.Any(s => s.Name == "Destruction"));
        }

        [Test]
        public void BlenderPropLodProgression_AcceptsStrictDecreasing()
        {
            bool accepted = AutoTestScenarioBlenderE2EProp.IsBlenderPropLodTriangleProgressionAcceptable(180, 96, 40, out bool strict);

            Assert.IsTrue(accepted);
            Assert.IsTrue(strict);
        }

        [Test]
        public void BlenderPropLodProgression_AcceptsFlatFallback()
        {
            bool accepted = AutoTestScenarioBlenderE2EProp.IsBlenderPropLodTriangleProgressionAcceptable(112, 112, 112, out bool strict);

            Assert.IsTrue(accepted);
            Assert.IsFalse(strict);
        }

        [Test]
        public void BlenderPropLodProgression_RejectsMixedNonDecreasing()
        {
            bool accepted = AutoTestScenarioBlenderE2EProp.IsBlenderPropLodTriangleProgressionAcceptable(140, 140, 120, out bool strict);

            Assert.IsFalse(accepted);
            Assert.IsFalse(strict);
        }

        [Test]
        public void GenericLodProgression_RejectsFlatFallback_WhenDisabled()
        {
            bool accepted = AutoTestScenarioBlenderE2EProp.IsLodTriangleProgressionAcceptable(112, 112, 112, allowFlatFallback: false, out bool strict);

            Assert.IsFalse(accepted);
            Assert.IsFalse(strict);
        }

        [Test]
        public void GenericLodProgression_AcceptsFlatFallback_WhenEnabled()
        {
            bool accepted = AutoTestScenarioBlenderE2EProp.IsLodTriangleProgressionAcceptable(112, 112, 112, allowFlatFallback: true, out bool strict);

            Assert.IsTrue(accepted);
            Assert.IsFalse(strict);
        }

        [Test]
        public void LargeTreeShowcaseMetrics_AcceptsAllRendererFallback_WhenLod0RendererArrayIsInvalid()
        {
            var root = new GameObject("Placed_LargeTree_Seedling");
            Mesh mesh = null;
            try
            {
                var lodGroup = root.AddComponent<LODGroup>();
                var meshChild = new GameObject("Visual");
                meshChild.transform.SetParent(root.transform, worldPositionStays: false);

                mesh = CreateSimpleBoundsMesh(new Vector3(2f, 4f, 2f));
                meshChild.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = meshChild.AddComponent<MeshRenderer>();

                lodGroup.SetLODs(new[]
                {
                    new LOD(0.7f, new Renderer[] { null }),
                    new LOD(0.4f, new[] { renderer }),
                    new LOD(0.1f, new[] { renderer }),
                });
                lodGroup.RecalculateBounds();

                bool ok = AutoTestScenarioLargeTree.TryGetLod0SilhouetteMetricsForTests(root, out float aspect, out float canopyFootprint);
                Assert.IsTrue(ok);
                Assert.Greater(aspect, 0.01f);
                Assert.Greater(canopyFootprint, 0.01f);
            }
            finally
            {
                if (mesh != null)
                    Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LargeTreeShowcaseMetrics_AcceptsMeshFallback_WhenNoRenderersExist()
        {
            var root = new GameObject("Placed_LargeTree_Seedling");
            Mesh mesh = null;
            try
            {
                var lodGroup = root.AddComponent<LODGroup>();
                var meshChild = new GameObject("VisualMeshOnly");
                meshChild.transform.SetParent(root.transform, worldPositionStays: false);

                mesh = CreateSimpleBoundsMesh(new Vector3(1.5f, 3f, 1.5f));
                meshChild.AddComponent<MeshFilter>().sharedMesh = mesh;

                lodGroup.SetLODs(new[]
                {
                    new LOD(0.7f, System.Array.Empty<Renderer>()),
                    new LOD(0.4f, System.Array.Empty<Renderer>()),
                    new LOD(0.1f, System.Array.Empty<Renderer>()),
                });
                lodGroup.RecalculateBounds();

                bool ok = AutoTestScenarioLargeTree.TryGetLod0SilhouetteMetricsForTests(root, out float aspect, out float canopyFootprint);
                Assert.IsTrue(ok);
                Assert.Greater(aspect, 0.01f);
                Assert.Greater(canopyFootprint, 0.01f);
            }
            finally
            {
                if (mesh != null)
                    Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
            }
        }

        private static Mesh CreateSimpleBoundsMesh(Vector3 size)
        {
            var mesh = new Mesh();
            float hx = size.x * 0.5f;
            float hz = size.z * 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-hx, 0f, -hz),
                new Vector3(hx, 0f, -hz),
                new Vector3(0f, size.y, hz),
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
