using ByteWar.UI;
using NUnit.Framework;

namespace ByteWar.Tests.EditMode
{
    public class AssetDeployUIShortcutTests
    {
        [TestCase(false, false, false)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, true)]
        public void IsOpenShortcutHeld_ReturnsExpected(bool shiftHeld, bool tabHeld, bool expected)
        {
            Assert.AreEqual(expected, AssetDeployUI.IsOpenShortcutHeld(shiftHeld, tabHeld));
        }

        [TestCase(false, false, false)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        public void ComputePanelVisible_ReturnsExpected(bool shortcutHeld, bool isDragging, bool expected)
        {
            Assert.AreEqual(expected, AssetDeployUI.ComputePanelVisible(shortcutHeld, isDragging));
        }

        [TestCase(false, false, false, false)]
        [TestCase(true, false, false, true)]
        [TestCase(false, true, false, true)]
        [TestCase(false, false, true, true)]
        [TestCase(true, true, true, true)]
        public void ComputeInteractionActive_ReturnsExpected(bool shortcutHeld, bool dragArmed, bool isDragging, bool expected)
        {
            Assert.AreEqual(expected, AssetDeployUI.ComputeInteractionActive(shortcutHeld, dragArmed, isDragging));
        }
    }
}
