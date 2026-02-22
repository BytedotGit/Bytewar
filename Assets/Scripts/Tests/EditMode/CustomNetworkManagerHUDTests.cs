using NUnit.Framework;
using SurvivalRPG.Networking;

namespace SurvivalRPG.Tests.EditMode
{
    public class CustomNetworkManagerHUDTests
    {
        [Test]
        public void ShouldAutoHost_ReturnsFalse_InEditor()
        {
            bool result = CustomNetworkManagerHUD.ShouldAutoHost(
                isEditor: true,
                isServer: false,
                isClient: false,
                args: new string[0]);

            Assert.False(result);
        }

        [Test]
        public void ShouldAutoHost_ReturnsTrue_InStandalone_NoArgs()
        {
            bool result = CustomNetworkManagerHUD.ShouldAutoHost(
                isEditor: false,
                isServer: false,
                isClient: false,
                args: new string[0]);

            Assert.True(result);
        }

        [Test]
        public void ShouldAutoHost_ReturnsFalse_WhenNoAutoHostFlagPresent()
        {
            bool result = CustomNetworkManagerHUD.ShouldAutoHost(
                isEditor: false,
                isServer: false,
                isClient: false,
                args: new[] { "-noAutoHost" });

            Assert.False(result);
        }

        [Test]
        public void ShouldAutoHost_ReturnsTrue_WhenAutoTestFlagPresent()
        {
            bool result = CustomNetworkManagerHUD.ShouldAutoHost(
                isEditor: false,
                isServer: false,
                isClient: false,
                args: new[] { "-autoTest" });

            Assert.True(result);
        }

        [Test]
        public void ShouldAutoHost_ReturnsFalse_WhenAlreadyServer()
        {
            bool result = CustomNetworkManagerHUD.ShouldAutoHost(
                isEditor: false,
                isServer: true,
                isClient: false,
                args: new string[0]);

            Assert.False(result);
        }

        [Test]
        public void ShouldAutoHost_ReturnsFalse_WhenAlreadyClient()
        {
            bool result = CustomNetworkManagerHUD.ShouldAutoHost(
                isEditor: false,
                isServer: false,
                isClient: true,
                args: new string[0]);

            Assert.False(result);
        }

        [Test]
        public void ShouldAutoHost_HandlesNullArgs()
        {
            bool result = CustomNetworkManagerHUD.ShouldAutoHost(
                isEditor: false,
                isServer: false,
                isClient: false,
                args: null);

            Assert.True(result);
        }
    }
}
