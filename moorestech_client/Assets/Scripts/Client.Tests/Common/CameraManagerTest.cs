using System.Collections.Generic;
using Client.Common;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.Common
{
    public class CameraManagerTest
    {
        private readonly List<GameObject> _cameraRoots = new();

        [SetUp]
        public void SetUp()
        {
            CameraManager.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in _cameraRoots) Object.DestroyImmediate(root);
            _cameraRoots.Clear();
        }

        [Test]
        public void 最上位カメラの変化だけを順に通知する()
        {
            var first = CreateCamera("First");
            var second = CreateCamera("Second");
            var notified = new List<IGameCamera>();
            CameraManager.OnMainCameraChanged.Subscribe(notified.Add);

            CameraManager.RegisterCamera(first);
            CameraManager.RegisterCamera(second);
            CameraManager.UnRegisterCamera(second);

            CollectionAssert.AreEqual(new[] { first, second, first }, notified);
            Assert.IsTrue(first.IsEnabled);
            Assert.IsFalse(second.IsEnabled);
        }

        [Test]
        public void 途中のカメラを外しても最上位変化を通知しない()
        {
            var first = CreateCamera("First");
            var second = CreateCamera("Second");
            var notified = new List<IGameCamera>();
            CameraManager.OnMainCameraChanged.Subscribe(notified.Add);
            CameraManager.RegisterCamera(first);
            CameraManager.RegisterCamera(second);
            notified.Clear();

            CameraManager.UnRegisterCamera(first);

            Assert.IsEmpty(notified);
            Assert.AreSame(second, CameraManager.MainCamera);
        }

        [Test]
        public void 最上位と同じカメラの再登録は通知しない()
        {
            var camera = CreateCamera("Camera");
            var notified = new List<IGameCamera>();
            CameraManager.RegisterCamera(camera);
            CameraManager.OnMainCameraChanged.Subscribe(notified.Add);

            CameraManager.RegisterCamera(camera);

            Assert.IsEmpty(notified);
        }

        private RecordingGameCamera CreateCamera(string name)
        {
            var root = new GameObject(name);
            _cameraRoots.Add(root);
            return new RecordingGameCamera(root.AddComponent<Camera>());
        }

        private sealed class RecordingGameCamera : IGameCamera
        {
            public Camera Camera { get; }
            public bool IsEnabled { get; private set; }

            public RecordingGameCamera(Camera camera)
            {
                Camera = camera;
            }

            public void SetEnabled(bool cameraEnabled)
            {
                IsEnabled = cameraEnabled;
            }
        }
    }
}
