using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Block
{
    // アイコン撮影の複数テストクラスが共有する命名規約・待機・後片付け
    // The naming rules, waiting, and cleanup shared by the icon capture test classes
    public static class BlockIconCaptureTestEnvironment
    {
        public const int CaptureCompletionFrameLimit = 30;
        public const string CaptureRenderTexturePrefix = "BlockIconCapture:";
        public const string TestObjectPrefix = "BlockIconLifetimeTest";

        // テスト生成のGameObjectと撮影用RenderTextureを名前で特定して破棄する
        // Destroy the test-created GameObjects and capture RenderTextures identified by name
        public static void DestroyTestObjects()
        {
            var objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var target in objects)
            {
                if (target == null) continue;
                if (target.name.StartsWith(TestObjectPrefix)) Object.DestroyImmediate(target);
            }

            var renderTextures = Resources.FindObjectsOfTypeAll<RenderTexture>();
            foreach (var renderTexture in renderTextures)
            {
                if (renderTexture.name.StartsWith(CaptureRenderTexturePrefix)) Object.DestroyImmediate(renderTexture);
            }
        }

        public static IEnumerator WaitForCompletion(UniTask<List<Texture2D>> captureTask)
        {
            for (var frame = 0; frame < CaptureCompletionFrameLimit && captureTask.Status == UniTaskStatus.Pending; frame++)
                yield return null;

            Assert.That(captureTask.Status, Is.Not.EqualTo(UniTaskStatus.Pending),
                $"Icon capture did not complete within {CaptureCompletionFrameLimit} frames.");
        }
    }
}
