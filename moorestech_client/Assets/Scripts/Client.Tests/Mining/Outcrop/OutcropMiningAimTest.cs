using System.Collections.Generic;
using System.Reflection;
using Client.Common;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.Mining;
using Client.Game.InGame.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Client.Tests.Mining.Outcrop
{
    /// <summary>
    ///     露頭解決と優先順を検証
    ///     Verify outcrop resolution and priority
    /// </summary>
    public class OutcropMiningAimTest : InputTestFixture
    {
        private readonly List<GameObject> _previousMainCameraObjects = new();
        private GameObject _cameraObject;
        private GameObject _eventSystemObject;
        private GameObject _playerObject;
        private GameObject _targetObject;
        private GameObject _miningObject;
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();

            // テストカメラだけがCamera.mainになるよう既存タグを一時退避する
            // Temporarily detach existing tags so the test camera alone becomes Camera.main
            foreach (var cameraObject in GameObject.FindGameObjectsWithTag("MainCamera"))
            {
                _previousMainCameraObjects.Add(cameraObject);
                cameraObject.tag = "Untagged";
            }
            CreateCameraAndEventSystem();
            CreatePlayerSystem();

            #region Internal

            void CreateCameraAndEventSystem()
            {
                _cameraObject = new GameObject("MainCamera");
                _cameraObject.tag = "MainCamera";
                _cameraObject.AddComponent<Camera>();

                // 本番UI判定を通す
                // Use production UI check
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

            // 他テストが所有するMainCameraタグを必ず元へ戻す
            // Restore every MainCamera tag owned by another test
            foreach (var cameraObject in _previousMainCameraObjects)
                if (cameraObject != null) cameraObject.tag = "MainCamera";
            _previousMainCameraObjects.Clear();
            base.TearDown();
        }

        [Test]
        public void 露頭マーカーのヒットは共通マーカー経由で露頭をフォーカスする()
        {
            var collider = CreateAimedCollider("OutcropTarget");
            var outcrop = collider.gameObject.AddComponent<OutcropGameObject>();
            collider.gameObject.AddComponent<OutcropRayTarget>().Initialize(outcrop);
            Physics.SyncTransforms();

            // 露頭マーカーから解決
            // Resolve from outcrop marker
            Assert.AreSame(outcrop, RunMiningUpdate().CurrentFocusTarget);
        }

        [Test]
        public void mapObjectマーカーのヒットは同じ共通マーカー経由でmapObjectをフォーカスする()
        {
            var collider = CreateAimedCollider("MapObjectTarget");
            var mapObject = collider.gameObject.AddComponent<MapObjectGameObject>();
            collider.gameObject.AddComponent<MapObjectRayTarget>().Initialize(mapObject, interactable: true);
            Physics.SyncTransforms();

            // 解決経路が対象種別によらず1本であることを固定する
            // Pin that the resolution path is a single one regardless of target kind
            Assert.AreSame(mapObject, RunMiningUpdate().CurrentFocusTarget);
        }

        private Collider CreateAimedCollider(string name)
        {
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Set(_mouse.position, center);
            var ray = _cameraObject.GetComponent<Camera>().ScreenPointToRay(center);
            _targetObject = new GameObject(name);
            _targetObject.layer = LayerConst.MapObjectLayer;
            _targetObject.transform.position = ray.GetPoint(1f);
            var sphereCollider = _targetObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.05f;
            return sphereCollider;
        }

        private MiningControllerContext RunMiningUpdate()
        {
            _playerObject.transform.position = _targetObject.transform.position;
            _miningObject = new GameObject("MiningController");
            var controller = _miningObject.AddComponent<MiningController>();
            var context = new MiningControllerContext(null);
            SetField(controller, "_context", context);
            SetField(controller, "_currentState", new StableMiningState());

            // private Updateで照準
            // Aim through private Update
            AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
            InvokePrivate(controller, "Update");
            return context;
        }

        private static void InvokePrivate(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void SetStaticProperty(System.Type targetType, string propertyName, object value)
        {
            targetType.GetField($"<{propertyName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        }

        private class StableMiningState : IMiningState
        {
            public IMiningState GetNextUpdate(MiningControllerContext context, float dt)
            {
                return this;
            }
        }
    }
}
