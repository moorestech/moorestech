using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Client.Game.InGame.Block;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Block
{
    public class BlockIconImagePhotographerLifetimeTest
    {
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
            LogAssert.Expect(LogType.Error, new Regex("^Destroy may not be called from edit mode!"));
            LogAssert.Expect(LogType.Error, new Regex("^Destroy may not be called from edit mode!"));
            var captureTask = photographer.TakeIconImages(new List<(GameObject prefab, string debugName)>
            {
                (targetPrefab, "lifetime-test"),
            });

            while (captureTask.Status == UniTaskStatus.Pending) yield return null;
            var textures = captureTask.GetAwaiter().GetResult();
            yield return null;

            var cameraCountAfter = CountCameras();
            foreach (var texture in textures) Object.DestroyImmediate(texture);
            Assert.That(cameraCountAfter, Is.EqualTo(cameraCountBefore));
        }

        private static int CountCameras()
        {
            return Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }
    }
}
