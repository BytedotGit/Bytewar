using NUnit.Framework;
using SurvivalRPG.Core;
using SurvivalRPG.Networking;
using System.Net;
using System.Net.Sockets;

namespace SurvivalRPG.Tests.EditMode
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
    }
}
