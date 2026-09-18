using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Block;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Block
{
    public class BlockIconImagePhotographerCaptureLogTest
    {
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

            BlockIconCaptureTestEnvironment.DestroyTestObjects();
        }

        [UnityTest]
        public IEnumerator TakeIconImages_撮影の段階をログに残す()
        {
            var prefix = BlockIconCaptureTestEnvironment.TestObjectPrefix;
            var photographerObject = new GameObject($"{prefix}LogPhotographer");
            var photographer = photographerObject.AddComponent<BlockIconImagePhotographer>();
            var cameraPrefabObject = new GameObject($"{prefix}LogCamera");
            var cameraPrefab = cameraPrefabObject.AddComponent<Camera>();
            var targetPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetPrefab.name = $"{prefix}LogTarget";
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
            yield return BlockIconCaptureTestEnvironment.WaitForCompletion(captureTask);
            var textures = captureTask.GetAwaiter().GetResult();
            Application.logMessageReceived -= _collectLog;
            _collectLog = null;
            foreach (var texture in textures) Object.DestroyImmediate(texture);

            Assert.That(captureLogs.Count, Is.EqualTo(12), string.Join(" | ", captureLogs));
            // 接頭辞リテラルは定数経由の突き合わせと別に固定し、定数値そのものの改変を検出する
            // Pin the prefix literal independently of the shared constant to catch a change to the constant's own value
            Assert.That(captureLogs[0], Does.StartWith("[BlockIconCapture] "));
            Assert.That(captureLogs[0], Is.EqualTo($"{BlockIconImagePhotographer.CaptureLogPrefix} start count:2"));
            AssertCaptureStages(captureLogs, 1, "1/2", captureDebugNameA);
            AssertCaptureStages(captureLogs, 6, "2/2", captureDebugNameB);
            Assert.That(captureLogs[11], Does.StartWith($"{BlockIconImagePhotographer.CaptureLogPrefix} completed count:2 elapsed:"));
        }

        // 1件分の5段（setup/render/readback/captured/done）が並び順どおり出ていることを検証する
        // Verify the five stages of one capture appear in order
        private static void AssertCaptureStages(List<string> captureLogs, int firstIndex, string progress, string captureDebugName)
        {
            var head = $"{BlockIconImagePhotographer.CaptureLogPrefix} {progress} {captureDebugName}";
            Assert.That(captureLogs[firstIndex], Is.EqualTo($"{head} stage:setup"));
            Assert.That(captureLogs[firstIndex + 1], Is.EqualTo($"{head} stage:render"));
            Assert.That(captureLogs[firstIndex + 2], Is.EqualTo($"{head} stage:readback"));
            Assert.That(captureLogs[firstIndex + 3], Is.EqualTo($"{head} stage:captured"));
            Assert.That(captureLogs[firstIndex + 4], Does.StartWith($"{head} stage:done elapsed:"));
        }
    }
}
