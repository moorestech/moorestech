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

            // 実Prefabと同じ参照を注入し、撮影前後のCamera総数を比較する
            // Inject the same reference as the real prefab and compare the total Camera count before and after capture
            var cameraField = typeof(BlockIconImagePhotographer).GetField("cameraPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            cameraField.SetValue(photographer, cameraPrefab);
            var cameraCountBefore = CountCameras();
            var captureTask = photographer.TakeIconImages(new List<(GameObject prefab, string debugName)>
            {
                (targetPrefab, "lifetime-test"),
            });

            yield return WaitForCompletion(captureTask);
            var textures = captureTask.GetAwaiter().GetResult();
            yield return null;

            var cameraCountAfter = CountCameras();
            foreach (var texture in textures) Object.DestroyImmediate(texture);
            Assert.That(cameraCountAfter, Is.EqualTo(cameraCountBefore));
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
            var cameraCountImmediatelyAfterStart = CountCameras();

            yield return WaitForCompletion(captureTask);
            var textures = captureTask.GetAwaiter().GetResult();
            yield return null;

            foreach (var texture in textures) Object.DestroyImmediate(texture);
            Assert.That(cameraCountImmediatelyAfterStart, Is.LessThanOrEqualTo(cameraCountBefore + 1));
        }

        private static int CountCameras()
        {
            return Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
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
