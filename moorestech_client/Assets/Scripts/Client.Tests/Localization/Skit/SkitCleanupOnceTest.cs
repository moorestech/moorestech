using System.IO;
using Client.Game.Skit.Lifecycle;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Localization.Skit
{
    public class SkitCleanupOnceTest
    {
        [Test]
        public void CleanupCanBeginOnlyOnce()
        {
            var cleanup = new SkitCleanupOnce();

            Assert.IsTrue(cleanup.TryBegin());
            Assert.IsFalse(cleanup.TryBegin());
        }

        [Test]
        public void SkitManagerUsesOuterFinallyForEveryRequiredCleanup()
        {
            var source = ReadSource("Scripts/Client.Game/Skit/SkitManager.cs");

            StringAssert.Contains("finally\n            {\n                Cleanup();\n            }", source);
            StringAssert.Contains("if (!cleanupOnce.TryBegin()) return;", source);
            AssertRequired(
                source,
                "skitUI.SetActive(false);",
                "SkitPresentationStateStore.Instance.End();",
                "mapObjectPin.SetActive(true);",
                "characterContainer?.DestroyAllCharacters();",
                "CameraManager.UnRegisterCamera(skitCamera);",
                "storyContext?.Dispose();",
                "localizationResolver?.Dispose();",
                "IsPlayingSkit = false;",
                "_isSkip = false;");
            StringAssert.DoesNotContain("catch", source);
        }

        [Test]
        public void BackgroundManagerUsesOuterFinallyForEveryRequiredCleanup()
        {
            var source = ReadSource(
                "Scripts/Client.Game/InGame/BackgroundSkit/BackgroundSkitManager.cs");

            StringAssert.Contains("finally\n            {\n                Cleanup();\n            }", source);
            StringAssert.Contains("if (!cleanupOnce.TryBegin()) return;", source);
            AssertRequired(
                source,
                "backgroundSkitUI.SetActive(false);",
                "SkitPresentationStateStore.Instance.End();",
                "context?.Dispose();",
                "localizationResolver?.Dispose();",
                "IsPlayingSkit = false;");
            StringAssert.DoesNotContain("catch", source);
        }

        private static string ReadSource(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, relativePath));
        }

        private static void AssertRequired(string source, params string[] requiredValues)
        {
            foreach (var requiredValue in requiredValues)
            {
                StringAssert.Contains(requiredValue, source);
            }
        }
    }
}
