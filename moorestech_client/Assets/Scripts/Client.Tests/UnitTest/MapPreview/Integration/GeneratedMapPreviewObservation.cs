using System.Collections;
using System.IO;
using Client.MapScene.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Client.Tests.UnitTest.MapPreview.Integration
{
    internal static class GeneratedMapPreviewObservation
    {
        internal static string[] WorldDirectories()
        {
            var root = Path.Combine(Application.dataPath, "..", "Temp", "GeneratedMapPreview", System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
            return Directory.Exists(root) ? Directory.GetDirectories(root) : System.Array.Empty<string>();
        }

        internal static IEnumerator WaitForCompletion(GeneratedMapPreviewStage stage)
        {
            // EditModeの更新を進めつつ、停止した生成は600秒で失敗として扱う
            // Advance EditMode updates while treating stalled generation as failure after 600 seconds
            var deadline = EditorApplication.timeSinceStartup + 600;
            while (stage.State == GeneratedMapPreviewState.Generating && EditorApplication.timeSinceStartup < deadline)
                yield return null;
            Assert.That(stage.State, Is.EqualTo(GeneratedMapPreviewState.Ready).Or.EqualTo(GeneratedMapPreviewState.Failed), stage.StatusText);
            Assert.That(EditorApplication.isPlaying, Is.False);
        }
    }
}
