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

            #region Internal

            void DetachExistingMainCameras()
            {
                // テストカメラをMainに固定
                // Make the test camera the sole Camera.main
                foreach (var cameraObject in GameObject.FindGameObjectsWithTag("MainCamera"))
                {
                    _previousMainCameraObjects.Add(cameraObject);
                    cameraObject.tag = "Untagged";
                }
            }

            void CreateCamera()
            {
                _cameraObject = new GameObject("MainCamera");
                _cameraObject.tag = "MainCamera";
                _cameraObject.AddComponent<Camera>();
            }

            void CreateEventSystem()
            {
                _eventSystemObject = new GameObject("EventSystem");
                var eventSystem = _eventSystemObject.AddComponent<EventSystem>();
                _eventSystemObject.AddComponent<InputSystemUIInputModule>();
                InvokePrivate(eventSystem, "OnEnable");
            }

            void CreatePlayerSystem()
            {
                _playerObject = new GameObject("PlayerSystem");
                var grabItemManager = _playerObject.AddComponent<PlayerGrabItemManager>();
                var playerController = _playerObject.AddComponent<PlayerObjectController>();
                var container = _playerObject.AddComponent<PlayerSystemContainer>();
                SetField(container, "playerGrabItemManager", grabItemManager);
                SetField(container, "playerObjectController", playerController);
                InvokePrivate(container, "Awake");
            }

            #endregion
        }

        public override void TearDown()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
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

            #region Internal

            static void SetStaticProperty(System.Type targetType, string propertyName, object value)
            {
                var field = targetType.GetField($"<{propertyName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
                field.SetValue(null, value);
            }

            #endregion
        }

        [Test]
        public void MiningUpdateUsesConfiguredMouseAndCenterAim()
        {
            var camera = _cameraObject.GetComponent<Camera>();
            var mousePoint = new Vector2(Screen.width / 2f + 200f, Screen.height / 2f + 100f);
            Set(_mouse.position, mousePoint);
            var expectedMapObject = CreateTarget(camera.ScreenPointToRay(mousePoint));
            _playerObject.transform.position = expectedMapObject.GetPosition();
            var controller = CreateMiningController();
            var context = new MapObjectMiningControllerContext(null, null);
            SetField(controller, "_context", context);
            SetField(controller, "_currentState", new StableMiningState());

            AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
            Assert.AreEqual(mousePoint, (Vector2)AimPointProvider.GetAimScreenPoint());
            InvokePrivate(controller, "Update");
            Assert.AreSame(expectedMapObject, context.CurrentFocusMapObjectGameObject);

            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            InvokePrivate(controller, "Update");
            Assert.IsNull(context.CurrentFocusMapObjectGameObject);

            #region Internal

            MapObjectGameObject CreateTarget(Ray ray)
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

            MapObjectMiningController CreateMiningController()
            {
                _miningObject = new GameObject("MapObjectMiningController");
                return _miningObject.AddComponent<MapObjectMiningController>();
            }

            #endregion
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

        private class StableMiningState : IMapObjectMiningState
        {
            public IMapObjectMiningState GetNextUpdate(MapObjectMiningControllerContext context, float dt)
            {
                return this;
            }
        }
    }
}
