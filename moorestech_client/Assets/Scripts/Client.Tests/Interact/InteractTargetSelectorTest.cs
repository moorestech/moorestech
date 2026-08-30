using System;
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Map.MapObject;
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

namespace Client.Tests.Interact
{
    /// <summary>
    ///     照準優先・近傍フォールバック・選定可否の3規則を検証（ADR 0046）
    ///     Verifies the three selection rules of ADR 0046: aim first, nearby fallback, availability gate
    /// </summary>
    public class InteractTargetSelectorTest : InputTestFixture
    {
        private static readonly Guid TreeMapObjectGuid = new("8c0e1339-be75-4690-99cd-58b5385a17cd");

        private readonly List<GameObject> _previousMainCameraObjects = new();
        private readonly List<GameObject> _targetObjects = new();
        private GameObject _cameraObject;
        private GameObject _eventSystemObject;
        private GameObject _playerObject;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Mouse>();

            // mapObjectの選定可否はマスタ解決済みかどうかで決まるため、実ローダーでマスタを用意する
            // Whether a map object is selectable depends on its resolved master, so load the real master
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

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
                // 本番UI判定を通す
                // Use the production UI check
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
            AimPointProvider.SetThirdPersonAimSource(ThirdPersonAimSource.ScreenCenter);
            TestReflection.SetStaticProperty(typeof(PlayerSystemContainer), "Instance", null);

            foreach (var targetObject in _targetObjects) UnityEngine.Object.DestroyImmediate(targetObject);
            _targetObjects.Clear();
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
        public void 照準レイのヒットが2m以内なら選ばれ2mを超えると選ばれない()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            var target = CreateMapObjectTarget(AimRay().GetPoint(1f));
            _playerObject.transform.position = target.transform.position;

            var selector = new InteractTargetSelector();
            Assert.AreSame(target, selector.Select());

            // 2mを超えると照準ヒットでも候補にならず、近傍にも無いのでnull
            // Beyond 2m the aim hit is discarded and nothing is nearby, so null
            _playerObject.transform.position = target.transform.position + new Vector3(0f, 0f, InteractTargetSelector.InteractDistance + 0.5f);
            Assert.IsNull(selector.Select());
        }

        [Test]
        public void 照準に何も無ければ半径2m内で視線角度が最小の候補が選ばれる()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            _cameraObject.transform.position = new Vector3(0f, 1f, -5f);
            _cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            _playerObject.transform.position = Vector3.zero;

            // 前方1.5m（角度0）と、より近い右横1.0m（角度90）
            // One 1.5m ahead (angle 0) and a closer one 1.0m to the right (angle 90)
            var ahead = CreateMapObjectTarget(new Vector3(0f, 0f, 1.5f));
            CreateMapObjectTarget(new Vector3(1.0f, 0f, 0f));

            Assert.AreSame(ahead, new InteractTargetSelector().Select());
        }

        [Test]
        public void マスタ未解決のmapObjectは照準に当たっても選ばれない()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            var target = CreateMapObjectTarget(AimRay().GetPoint(1f));
            _playerObject.transform.position = target.transform.position;

            // 選定はIsInteractAvailableを通る。マスタ未解決なら照準に当たっていても対象にならない
            // Selection passes through IsInteractAvailable, so a master-less object under the aim is no target
            TestReflection.SetField(target, "<MapObjectMasterElement>k__BackingField", null);
            Assert.IsNull(new InteractTargetSelector().Select());
        }

        [Test]
        public void 開けないブロックは照準に当たっても選ばれない()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);

            // インタラクト面が付かないブロック（ベルトコンベア等）は解決先が無い
            // A block with no interact face attached, such as a belt conveyor, resolves to nothing
            var blockObject = new GameObject("PlainBlock");
            blockObject.transform.position = AimRay().GetPoint(1f);
            var blockGameObject = blockObject.AddComponent<BlockGameObject>();
            _targetObjects.Add(blockObject);

            var meshChild = new GameObject("BlockMesh") { layer = LayerConst.BlockLayer };
            meshChild.transform.SetParent(blockObject.transform, false);
            meshChild.AddComponent<SphereCollider>().radius = 0.05f;
            meshChild.AddComponent<BlockGameObjectChild>().Init(blockGameObject);

            _playerObject.transform.position = blockObject.transform.position;
            Physics.SyncTransforms();

            Assert.IsNull(new InteractTargetSelector().Select());
        }

        private Ray AimRay()
        {
            var camera = _cameraObject.GetComponent<Camera>();
            return camera.ScreenPointToRay(new Vector2(Screen.width / 2f, Screen.height / 2f));
        }

        private MapObjectGameObject CreateMapObjectTarget(Vector3 position)
        {
            var targetObject = new GameObject("MapObjectTarget") { layer = LayerConst.MapObjectLayer };
            targetObject.transform.position = position;
            targetObject.AddComponent<SphereCollider>().radius = 0.05f;
            var mapObject = targetObject.AddComponent<MapObjectGameObject>();
            targetObject.AddComponent<MapObjectRayTarget>().Initialize(mapObject);

            // マスタ解決済みのmapObjectだけが選定対象になるため、実マスタの要素を載せる
            // Only a map object with a resolved master is selectable, so put a real master element on it
            TestReflection.SetField(mapObject, "<MapObjectMasterElement>k__BackingField", MasterHolder.MapObjectMaster.GetMapObjectElement(TreeMapObjectGuid));

            _targetObjects.Add(targetObject);
            Physics.SyncTransforms();
            return mapObject;
        }
    }
}
