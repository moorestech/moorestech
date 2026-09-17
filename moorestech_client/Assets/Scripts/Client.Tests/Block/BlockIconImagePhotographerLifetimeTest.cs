using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Block;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Block
{
    public class BlockIconImagePhotographerLifetimeTest
    {
        private const int CaptureCompletionFrameLimit = 30;
        private const string CaptureRenderTexturePrefix = "BlockIconCapture:";
        private const string TestObjectPrefix = "BlockIconLifetimeTest";

        [TearDown]
        public void TearDown()
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

        [UnityTest]
        public IEnumerator TakeIconImages_撮影用Cameraを残さない()
        {
            var photographerObject = new GameObject($"{TestObjectPrefix}Photographer");
            var photographer = photographerObject.AddComponent<BlockIconImagePhotographer>();
            var cameraPrefabObject = new GameObject($"{TestObjectPrefix}Camera");
            var cameraPrefab = cameraPrefabObject.AddComponent<Camera>();
            var targetPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetPrefab.name = $"{TestObjectPrefix}Target";
            const string captureDebugName = "lifetime-test";

            // 実Prefabと同じ参照を注入し、撮影前後のCamera総数を比較する
            // Inject the same reference as the real prefab and compare the total Camera count before and after capture
            var cameraField = typeof(BlockIconImagePhotographer).GetField("cameraPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            cameraField.SetValue(photographer, cameraPrefab);
            var cameraCountBefore = CountCameras();
            var targetInstanceCountBefore = CountTargetInstances(targetPrefab.name);
            var renderTextureCountBefore = CountRenderTextures(captureDebugName);
            var captureTask = photographer.TakeIconImages(new List<(GameObject prefab, string debugName)>
            {
                (targetPrefab, captureDebugName),
            });

            yield return WaitForCompletion(captureTask);
            var textures = captureTask.GetAwaiter().GetResult();
            yield return null;

            var cameraCountAfter = CountCameras();
            var targetInstanceCountAfter = CountTargetInstances(targetPrefab.name);
            var renderTextureCountAfter = CountRenderTextures(captureDebugName);
            foreach (var texture in textures) Object.DestroyImmediate(texture);
            Assert.That(cameraCountAfter, Is.EqualTo(cameraCountBefore));
            Assert.That(targetInstanceCountAfter, Is.EqualTo(targetInstanceCountBefore));
            Assert.That(renderTextureCountAfter, Is.EqualTo(renderTextureCountBefore));
        }

        [UnityTest]
        public IEnumerator TakeIconImages_撮影Cameraを一台ずつ生成する()
        {
            var photographerObject = new GameObject($"{TestObjectPrefix}SequentialPhotographer");
            var photographer = photographerObject.AddComponent<BlockIconImagePhotographer>();
            var cameraPrefabObject = new GameObject($"{TestObjectPrefix}SequentialCamera");
            var cameraPrefab = cameraPrefabObject.AddComponent<Camera>();
            var targetPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetPrefab.name = $"{TestObjectPrefix}SequentialTarget";

            // 複数対象の呼び出し直後に、生存する撮影Cameraの上限を検証する
            // Verify the capture Camera limit immediately after starting multiple subjects
            var cameraField = typeof(BlockIconImagePhotographer).GetField("cameraPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            cameraField.SetValue(photographer, cameraPrefab);
            var cameraCountBefore = CountCameras();
            var captureTask = photographer.TakeIconImages(new List<(GameObject prefab, string debugName)>
            {
                (targetPrefab, "sequential-test-a"),
                (targetPrefab, "sequential-test-b"),
            });
            var peakCameraCount = CountCameras();

            for (var frame = 0; frame < CaptureCompletionFrameLimit && captureTask.Status == UniTaskStatus.Pending; frame++)
            {
                peakCameraCount = Mathf.Max(peakCameraCount, CountCameras());
                yield return null;
            }
            peakCameraCount = Mathf.Max(peakCameraCount, CountCameras());
            Assert.That(captureTask.Status, Is.Not.EqualTo(UniTaskStatus.Pending),
                $"Icon capture did not complete within {CaptureCompletionFrameLimit} frames.");
            var textures = captureTask.GetAwaiter().GetResult();
            yield return null;

            foreach (var texture in textures) Object.DestroyImmediate(texture);
            Assert.That(peakCameraCount, Is.LessThanOrEqualTo(cameraCountBefore + 1));
        }

        [UnityTest]
        public IEnumerator TakeIconImages_撮影の段階をログに残す()
        {
            var photographerObject = new GameObject($"{TestObjectPrefix}LogPhotographer");
            var photographer = photographerObject.AddComponent<BlockIconImagePhotographer>();
            var cameraPrefabObject = new GameObject($"{TestObjectPrefix}LogCamera");
            var cameraPrefab = cameraPrefabObject.AddComponent<Camera>();
            var targetPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetPrefab.name = $"{TestObjectPrefix}LogTarget";
            const string captureDebugName = "log-test";

            var cameraField = typeof(BlockIconImagePhotographer).GetField("cameraPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            cameraField.SetValue(photographer, cameraPrefab);

            // 固着時はメインスレッドごと止まるため、各段階へ入る直前のログだけが箇所の手掛かりになる
            // A freeze stops the main thread itself, so only the log emitted before each stage can locate it
            var captureLogs = new List<string>();
            void CollectLog(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Log && condition.StartsWith(BlockIconImagePhotographer.CaptureLogPrefix)) captureLogs.Add(condition);
            }

            Application.logMessageReceived += CollectLog;
            var captureTask = photographer.TakeIconImages(new List<(GameObject prefab, string debugName)>
            {
                (targetPrefab, captureDebugName),
            });
            yield return WaitForCompletion(captureTask);
            var textures = captureTask.GetAwaiter().GetResult();
            Application.logMessageReceived -= CollectLog;
            foreach (var texture in textures) Object.DestroyImmediate(texture);

            Assert.That(captureLogs.Count, Is.EqualTo(5), string.Join(" | ", captureLogs));
            Assert.That(captureLogs[0], Is.EqualTo($"{BlockIconImagePhotographer.CaptureLogPrefix} start count:1"));
            Assert.That(captureLogs[1], Is.EqualTo($"{BlockIconImagePhotographer.CaptureLogPrefix} 1/1 {captureDebugName} stage:render"));
            Assert.That(captureLogs[2], Is.EqualTo($"{BlockIconImagePhotographer.CaptureLogPrefix} 1/1 {captureDebugName} stage:readback"));
            Assert.That(captureLogs[3], Does.StartWith($"{BlockIconImagePhotographer.CaptureLogPrefix} 1/1 {captureDebugName} stage:done elapsed:"));
            Assert.That(captureLogs[4], Does.StartWith($"{BlockIconImagePhotographer.CaptureLogPrefix} completed count:1 elapsed:"));
        }

        private static int CountCameras()
        {
            return Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }

        private static int CountTargetInstances(string prefabName)
        {
            var count = 0;
            var expectedName = $"{prefabName}(Clone)";
            var objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var target in objects)
            {
                if (target.name == expectedName) count++;
            }

            return count;
        }

        private static int CountRenderTextures(string captureDebugName)
        {
            var count = 0;
            var expectedName = $"{CaptureRenderTexturePrefix}{captureDebugName}";
            var renderTextures = Resources.FindObjectsOfTypeAll<RenderTexture>();
            foreach (var renderTexture in renderTextures)
            {
                if (renderTexture.name == expectedName) count++;
            }

            return count;
        }

        private static IEnumerator WaitForCompletion(UniTask<List<Texture2D>> captureTask)
        {
            for (var frame = 0; frame < CaptureCompletionFrameLimit && captureTask.Status == UniTaskStatus.Pending; frame++)
                yield return null;

            Assert.That(captureTask.Status, Is.Not.EqualTo(UniTaskStatus.Pending),
                $"Icon capture did not complete within {CaptureCompletionFrameLimit} frames.");
        }
    }
}
