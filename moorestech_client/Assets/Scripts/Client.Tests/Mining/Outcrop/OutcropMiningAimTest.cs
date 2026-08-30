using System;
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.Player;
using Client.Tests.Common;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
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
        private static readonly Guid TreeMapObjectGuid = new("8c0e1339-be75-4690-99cd-58b5385a17cd");

        private readonly List<GameObject> _previousMainCameraObjects = new();
        private GameObject _cameraObject;
        private GameObject _eventSystemObject;
        private GameObject _playerObject;
        private GameObject _targetObject;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Mouse>();

            // mapObjectの選定可否はマスタ解決済みかどうかで決まるため、実ローダーでマスタを用意する
            // Whether a map object is selectable depends on its resolved master, so load the real master
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

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
                TestReflection.InvokePrivate(eventSystem, "OnEnable");
            }

            void CreatePlayerSystem()
            {
                _playerObject = new GameObject("PlayerSystem");
                var grabItemManager = _playerObject.AddComponent<PlayerGrabItemManager>();
                var playerController = _playerObject.AddComponent<PlayerObjectController>();
                var container = _playerObject.AddComponent<PlayerSystemContainer>();
                TestReflection.SetField(container, "playerGrabItemManager", grabItemManager);
                TestReflection.SetField(container, "playerObjectController", playerController);
                TestReflection.InvokePrivate(container, "Awake");
            }

            #endregion
        }

        public override void TearDown()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
            TestReflection.SetStaticProperty(typeof(PlayerSystemContainer), "Instance", null);
            UnityEngine.Object.DestroyImmediate(_targetObject);
            UnityEngine.Object.DestroyImmediate(_playerObject);
            UnityEngine.Object.DestroyImmediate(_eventSystemObject);
            UnityEngine.Object.DestroyImmediate(_cameraObject);

            // 他テストが所有するMainCameraタグを必ず元へ戻す
            // Restore every MainCamera tag owned by another test
            foreach (var cameraObject in _previousMainCameraObjects)
                if (cameraObject != null) cameraObject.tag = "MainCamera";
            _previousMainCameraObjects.Clear();
            base.TearDown();
        }

        [Test]
        public void 露頭マーカーのヒットは共通マーカー経由で露頭が選ばれる()
        {
            var collider = CreateAimedCollider("OutcropTarget");
            var outcrop = collider.gameObject.AddComponent<OutcropGameObject>();
            collider.gameObject.AddComponent<OutcropRayTarget>().Initialize(outcrop);
            Physics.SyncTransforms();

            // 露頭マーカーから解決
            // Resolve from outcrop marker
            Assert.AreSame(outcrop, SelectUnderAim());
        }

        [Test]
        public void mapObjectマーカーのヒットは同じ共通マーカー経由でmapObjectが選ばれる()
        {
            var collider = CreateAimedCollider("MapObjectTarget");
            var mapObject = collider.gameObject.AddComponent<MapObjectGameObject>();
            collider.gameObject.AddComponent<MapObjectRayTarget>().Initialize(mapObject);
            TestReflection.SetField(mapObject, "<MapObjectMasterElement>k__BackingField", MasterHolder.MapObjectMaster.GetMapObjectElement(TreeMapObjectGuid));
            Physics.SyncTransforms();

            // 解決経路が対象種別によらず1本であることを固定する
            // Pin that the resolution path is a single one regardless of target kind
            Assert.AreSame(mapObject, SelectUnderAim());
        }

        private Collider CreateAimedCollider(string name)
        {
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var ray = _cameraObject.GetComponent<Camera>().ScreenPointToRay(center);
            _targetObject = new GameObject(name);
            _targetObject.layer = LayerConst.MapObjectLayer;
            _targetObject.transform.position = ray.GetPoint(1f);
            var sphereCollider = _targetObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.05f;
            return sphereCollider;
        }

        private IInteractable SelectUnderAim()
        {
            _playerObject.transform.position = _targetObject.transform.position;
            AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
            return new InteractTargetSelector().Select();
        }
    }
}
