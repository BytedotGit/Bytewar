using NUnit.Framework;
using SurvivalRPG.Core;
using SurvivalRPG.Networking;

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
    }
}
