using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Client.Game.InGame.Map.MapObject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Map.Visibility
{
    public class MapObjectDistanceVisibilityControllerTest
    {
        private readonly List<GameObject> _roots = new();
        private readonly List<MapObjectDistanceVisibilityController> _controllers = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var controller in _controllers) controller.Shutdown();
            foreach (var root in _roots) Object.DestroyImmediate(root);
            _controllers.Clear();
            _roots.Clear();
        }

        [UnityTest]
        public IEnumerator 通常個体は350m以遠で非表示になる()
        {
            var camera = CreateCamera(Vector3.zero);
            var controller = CreateController(1, camera);
            var mapObject = CreateMapObject(new Vector3(400f, 0f, 0f), out var renderer);

            controller.Register(mapObject, false);
            yield return null;
            yield return null;

            Assert.IsFalse(renderer.enabled);
        }

        [UnityTest]
        public IEnumerator 遠景ランドマークは350m以遠でも表示を維持する()
        {
            var camera = CreateCamera(Vector3.zero);
            var controller = CreateController(1, camera);
            var mapObject = CreateMapObject(new Vector3(400f, 0f, 0f), out var renderer);

            controller.Register(mapObject, true);
            yield return null;

            Assert.IsTrue(renderer.enabled);
        }

        [UnityTest]
        public IEnumerator 中間bandでは現在状態を維持して内側で再表示する()
        {
            var camera = CreateCamera(Vector3.zero);
            var controller = CreateController(1, camera);
            var mapObject = CreateMapObject(Vector3.zero, out var renderer);
            controller.Register(mapObject, false);
            yield return null;

            controller.ApplyDistanceBand(0, 2);
            yield return null;
            Assert.IsFalse(renderer.enabled);

            controller.ApplyDistanceBand(0, 1);
            yield return null;
            Assert.IsFalse(renderer.enabled);

            controller.ApplyDistanceBand(0, 0);
            yield return null;
            Assert.IsTrue(renderer.enabled);
        }

        [UnityTest]
        public IEnumerator カメラ切替時に新しい距離基準へ表示を揃える()
        {
            var nearCamera = CreateCamera(new Vector3(400f, 0f, 0f));
            var farCamera = CreateCamera(Vector3.zero);
            var controller = CreateController(1, nearCamera);
            var mapObject = CreateMapObject(new Vector3(400f, 0f, 0f), out var renderer);
            controller.Register(mapObject, false);
            yield return null;

            controller.SetCamera(farCamera);
            yield return null;
            yield return null;

            Assert.IsFalse(renderer.enabled);
        }

        [UnityTest]
        public IEnumerator 同じframeの更新は最新の表示要求だけを反映する()
        {
            var camera = CreateCamera(Vector3.zero);
            var controller = CreateController(1, camera);
            var mapObject = CreateMapObject(Vector3.zero, out var renderer);
            controller.Register(mapObject, false);
            yield return null;

            controller.ApplyDistanceBand(0, 2);
            controller.ApplyDistanceBand(0, 0);
            yield return null;
            yield return null;

            Assert.IsTrue(renderer.enabled);
        }

        [UnityTest]
        public IEnumerator 距離bandが変わらないculling通知はqueueへ積まない()
        {
            var camera = CreateCamera(Vector3.zero);
            var controller = CreateController(1, camera);
            var mapObject = CreateMapObject(Vector3.zero, out _);
            controller.Register(mapObject, false);
            yield return null;
            yield return null;
            Assert.AreEqual(0, PendingIndexCount(controller));

            // 視錐台の出入りだけの通知は距離bandが同じなので破棄される
            // A frustum-only notification keeps the distance band and must be discarded
            RaiseStateChanged(controller, 0, 2, 2);
            Assert.AreEqual(0, PendingIndexCount(controller));

            RaiseStateChanged(controller, 0, 2, 0);
            Assert.AreEqual(1, PendingIndexCount(controller));
        }

        private MapObjectDistanceVisibilityController CreateController(int capacity, Camera camera)
        {
            var controller = new MapObjectDistanceVisibilityController(capacity, CancellationToken.None);
            _controllers.Add(controller);
            controller.SetCamera(camera);
            return controller;
        }

        private Camera CreateCamera(Vector3 position)
        {
            var root = new GameObject("MapObjectDistanceVisibilityCamera");
            _roots.Add(root);
            root.transform.position = position;
            return root.AddComponent<Camera>();
        }

        private static int PendingIndexCount(MapObjectDistanceVisibilityController controller)
        {
            var field = typeof(MapObjectDistanceVisibilityController)
                .GetField("_pendingIndices", BindingFlags.Instance | BindingFlags.NonPublic);
            return ((Queue<int>)field.GetValue(controller)).Count;
        }

        private static void RaiseStateChanged(MapObjectDistanceVisibilityController controller, int index, int previousDistance, int currentDistance)
        {
            var method = typeof(MapObjectDistanceVisibilityController)
                .GetMethod("OnStateChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(controller, new object[] { CreateStateChangedEvent(index, previousDistance, currentDistance) });
        }

        private static CullingGroupEvent CreateStateChangedEvent(int index, int previousDistance, int currentDistance)
        {
            // CullingGroupEventはnativeだけが組み立てるため、privateフィールドを直接埋めて再現する
            // Only native code builds CullingGroupEvent, so fill its private fields to reproduce one
            var fields = typeof(CullingGroupEvent).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.AreEqual(3, fields.Length);
            object boxed = default(CullingGroupEvent);
            fields[0].SetValue(boxed, index);
            fields[1].SetValue(boxed, (byte)previousDistance);
            fields[2].SetValue(boxed, (byte)currentDistance);

            // layoutが変わったら黙って通り抜けないよう、公開値で組み立て結果を検証する
            // Verify through public values so a layout change fails loudly instead of passing silently
            var sphereEvent = (CullingGroupEvent)boxed;
            Assert.AreEqual(index, sphereEvent.index);
            Assert.AreEqual(previousDistance, sphereEvent.previousDistance);
            Assert.AreEqual(currentDistance, sphereEvent.currentDistance);
            return sphereEvent;
        }

        private MapObjectGameObject CreateMapObject(Vector3 position, out MeshRenderer renderer)
        {
            var root = new GameObject("MapObjectDistanceVisibilityTarget");
            _roots.Add(root);
            root.transform.position = position;
            var mapObject = root.AddComponent<MapObjectGameObject>();
            renderer = root.AddComponent<MeshRenderer>();
            return mapObject;
        }
    }
}
