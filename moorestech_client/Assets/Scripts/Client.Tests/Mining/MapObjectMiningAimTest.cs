using System.Collections.Generic;
using System.Reflection;
using Client.Common;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Mining;
using Client.Game.InGame.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Client.Tests.Mining
{
    public class MapObjectMiningAimTest : InputTestFixture
    {
        private GameObject _cameraObject;
        private GameObject _eventSystemObject;
        private GameObject _playerObject;
        private GameObject _targetObject;
        private GameObject _miningObject;
        private Mouse _mouse;
        private readonly List<GameObject> _previousMainCameraObjects = new();

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            DetachExistingMainCameras();
            CreateCamera();
            CreateEventSystem();
            CreatePlayerSystem();
        }

        public override void TearDown()
        {
            AimPointProvider.SetMode(PlayerViewMode.ThirdPerson);
            SetStaticProperty(typeof(PlayerSystemContainer), "Instance", null);
            Object.DestroyImmediate(_miningObject);
            Object.DestroyImmediate(_targetObject);
            Object.DestroyImmediate(_playerObject);
            Object.DestroyImmediate(_eventSystemObject);
            Object.DestroyImmediate(_cameraObject);
            foreach (var cameraObject in _previousMainCameraObjects)
                if (cameraObject != null) cameraObject.tag = "MainCamera";
            _previousMainCameraObjects.Clear();
            base.TearDown();
        }

        [Test]
        public void MiningTargetUsesMouseAimInThirdPersonAndCenterAimInFirstPerson()
        {
            var camera = _cameraObject.GetComponent<Camera>();
            var mousePoint = new Vector2(Screen.width / 2f + 200f, Screen.height / 2f + 100f);
            Set(_mouse.position, mousePoint);
            var expectedMapObject = CreateTarget(camera.ScreenPointToRay(mousePoint));
            _playerObject.transform.position = expectedMapObject.GetPosition();
            var controller = CreateMiningController();

            AimPointProvider.SetMode(PlayerViewMode.ThirdPerson);
            Assert.AreEqual(mousePoint, (Vector2)AimPointProvider.GetAimScreenPoint());
            Assert.AreSame(expectedMapObject, InvokeGetCurrentMapObject(controller));

            AimPointProvider.SetMode(PlayerViewMode.FirstPerson);
            Assert.IsNull(InvokeGetCurrentMapObject(controller));
        }

        private void DetachExistingMainCameras()
        {
            // テストカメラをMainに固定
            // Make the test camera the sole Camera.main
            foreach (var cameraObject in GameObject.FindGameObjectsWithTag("MainCamera"))
            {
                _previousMainCameraObjects.Add(cameraObject);
                cameraObject.tag = "Untagged";
            }
        }

        private void CreateCamera()
        {
            _cameraObject = new GameObject("MainCamera");
            _cameraObject.tag = "MainCamera";
            _cameraObject.AddComponent<Camera>();
        }

        private void CreateEventSystem()
        {
            _eventSystemObject = new GameObject("EventSystem");
            var eventSystem = _eventSystemObject.AddComponent<EventSystem>();
            _eventSystemObject.AddComponent<InputSystemUIInputModule>();
            InvokePrivate(eventSystem, "OnEnable");
        }

        private void CreatePlayerSystem()
        {
            _playerObject = new GameObject("PlayerSystem");
            var grabItemManager = _playerObject.AddComponent<PlayerGrabItemManager>();
            var playerController = _playerObject.AddComponent<PlayerObjectController>();
            var container = _playerObject.AddComponent<PlayerSystemContainer>();
            SetField(container, "playerGrabItemManager", grabItemManager);
            SetField(container, "playerObjectController", playerController);
            InvokePrivate(container, "Awake");
        }

        private MapObjectGameObject CreateTarget(Ray ray)
        {
            _targetObject = new GameObject("MapObjectTarget");
            _targetObject.layer = LayerConst.MapObjectLayer;
            _targetObject.transform.position = ray.GetPoint(1f);
            _targetObject.AddComponent<SphereCollider>().radius = 0.05f;
            var mapObject = _targetObject.AddComponent<MapObjectGameObject>();
            _targetObject.AddComponent<MapObjectRayTarget>().Initialize(mapObject);
            Physics.SyncTransforms();
            return mapObject;
        }

        private MapObjectMiningController CreateMiningController()
        {
            _miningObject = new GameObject("MapObjectMiningController");
            return _miningObject.AddComponent<MapObjectMiningController>();
        }

        private static MapObjectGameObject InvokeGetCurrentMapObject(MapObjectMiningController controller)
        {
            var method = typeof(MapObjectMiningController).GetMethod("GetCurrentMapObject", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (MapObjectGameObject)method.Invoke(controller, null);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        private static void SetStaticProperty(System.Type targetType, string propertyName, object value)
        {
            var field = targetType.GetField($"<{propertyName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            field.SetValue(null, value);
        }
    }
}
