using NUnit.Framework;
using System.IO;
using ByteWar.Editor;

namespace ByteWar.Tests.EditMode
{
    public class ProjectInputHandlingTests
    {
        [Test]
        public void ProjectSettings_ActiveInputHandler_IsBoth()
        {
            string path = Path.GetFullPath("ProjectSettings/ProjectSettings.asset");
            Assert.IsTrue(File.Exists(path), $"ProjectSettings.asset not found at '{path}'.");

            string text = File.ReadAllText(path);
            Assert.IsTrue(InputHandlingUtility.TryGetActiveInputHandler(text, out int value),
                "activeInputHandler not found in ProjectSettings.asset.");

            Assert.AreEqual(InputHandlingUtility.Both, value,
                "Active Input Handling must be 'Both' (2) or the new Input System won't drive movement/camera.");
        }
    }
}
