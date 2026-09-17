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

        private Application.LogCallback _collectLog;

        [TearDown]
        public void TearDown()
        {
            // 撮影失敗時もテスト間で購読が積み上がらないよう無条件で解除する
            // Unsubscribe unconditionally so a failed capture never leaves the subscription across tests
            if (_collectLog != null)
            {
                Application.logMessageReceived -= _collectLog;
                _collectLog = null;
            }

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
            const string captureDebugNameA = "log-test-a";
            const string captureDebugNameB = "log-test-b";

            var cameraField = typeof(BlockIconImagePhotographer).GetField("cameraPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            cameraField.SetValue(photographer, cameraPrefab);

            // 固着時はメインスレッドごと止まるため、各段階へ入る直前のログだけが箇所の手掛かりになる
            // A freeze stops the main thread itself, so only the log emitted before each stage can locate it
            var captureLogs = new List<string>();
            _collectLog = (condition, stackTrace, type) =>
            {
                if (type == LogType.Log && condition.StartsWith(BlockIconImagePhotographer.CaptureLogPrefix)) captureLogs.Add(condition);
            };

            Application.logMessageReceived += _collectLog;
            var captureTask = photographer.TakeIconImages(new List<(GameObject prefab, string debugName)>
            {
                (targetPrefab, captureDebugNameA),
                (targetPrefab, captureDebugNameB),
            });
            yield return WaitForCompletion(captureTask);
            var textures = captureTask.GetAwaiter().GetResult();
            Application.logMessageReceived -= _collectLog;
            _collectLog = null;
            foreach (var texture in textures) Object.DestroyImmediate(texture);

            Assert.That(captureLogs.Count, Is.EqualTo(8), string.Join(" | ", captureLogs));
            // 接頭辞リテラルは定数経由の突き合わせと別に固定し、定数値そのものの改変を検出する
            // Pin the prefix literal independently of the shared constant to catch a change to the constant's own value
            Assert.That(captureLogs[0], Does.StartWith("[BlockIconCapture] "));
            Assert.That(captureLogs[0], Is.EqualTo($"{BlockIconImagePhotographer.CaptureLogPrefix} start count:2"));
            Assert.That(captureLogs[1], Is.EqualTo($"{BlockIconImagePhotographer.CaptureLogPrefix} 1/2 {captureDebugNameA} stage:render"));
            Assert.That(captureLogs[2], Is.EqualTo($"{BlockIconImagePhotographer.CaptureLogPrefix} 1/2 {captureDebugNameA} stage:readback"));
            Assert.That(captureLogs[3], Does.StartWith($"{BlockIconImagePhotographer.CaptureLogPrefix} 1/2 {captureDebugNameA} stage:done elapsed:"));
            Assert.That(captureLogs[4], Is.EqualTo($"{BlockIconImagePhotographer.CaptureLogPrefix} 2/2 {captureDebugNameB} stage:render"));
            Assert.That(captureLogs[5], Is.EqualTo($"{BlockIconImagePhotographer.CaptureLogPrefix} 2/2 {captureDebugNameB} stage:readback"));
            Assert.That(captureLogs[6], Does.StartWith($"{BlockIconImagePhotographer.CaptureLogPrefix} 2/2 {captureDebugNameB} stage:done elapsed:"));
            Assert.That(captureLogs[7], Does.StartWith($"{BlockIconImagePhotographer.CaptureLogPrefix} completed count:2 elapsed:"));
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
