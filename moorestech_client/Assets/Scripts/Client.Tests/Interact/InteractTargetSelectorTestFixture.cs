using System;
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Player;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Tests.Common;
using Core.Master;
using Game.Train.Unit;
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
    ///     選定テストの土台（実カメラ・EventSystem等）
    ///     Shared ground for the selection tests: real camera, EventSystem, master data and physics
    /// </summary>
    public abstract class InteractTargetSelectorTestFixture : InputTestFixture
    {
        protected static readonly Guid TreeMapObjectGuid = new("8c0e1339-be75-4690-99cd-58b5385a17cd");

        private readonly List<GameObject> _previousMainCameraObjects = new();
        protected readonly List<GameObject> TargetObjects = new();
        protected GameObject CameraObject;
        protected GameObject PlayerObject;
        private GameObject _eventSystemObject;

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
                CameraObject = new GameObject("MainCamera");
                CameraObject.tag = "MainCamera";
                CameraObject.AddComponent<Camera>();
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
                PlayerObject = new GameObject("PlayerSystem");
                var grabItemManager = PlayerObject.AddComponent<PlayerGrabItemManager>();
                var playerController = PlayerObject.AddComponent<PlayerObjectController>();
                var container = PlayerObject.AddComponent<PlayerSystemContainer>();
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

            foreach (var targetObject in TargetObjects) UnityEngine.Object.DestroyImmediate(targetObject);
            TargetObjects.Clear();
            UnityEngine.Object.DestroyImmediate(PlayerObject);
            UnityEngine.Object.DestroyImmediate(_eventSystemObject);
            UnityEngine.Object.DestroyImmediate(CameraObject);

            // 他テストのMainCameraタグを復元
            // Restore every MainCamera tag owned by another test
            foreach (var cameraObject in _previousMainCameraObjects)
                if (cameraObject != null) cameraObject.tag = "MainCamera";
            _previousMainCameraObjects.Clear();
            base.TearDown();
        }

        protected Ray AimRay()
        {
            var camera = CameraObject.GetComponent<Camera>();
            return camera.ScreenPointToRay(new Vector2(Screen.width / 2f, Screen.height / 2f));
        }

        protected MapObjectGameObject CreateMapObjectTarget(Vector3 position)
        {
            var targetObject = new GameObject("MapObjectTarget") { layer = LayerConst.MapObjectLayer };
            targetObject.transform.position = position;
            targetObject.AddComponent<SphereCollider>().radius = 0.05f;
            var mapObject = targetObject.AddComponent<MapObjectGameObject>();
            targetObject.AddComponent<MapObjectRayTarget>().Initialize(mapObject);

            // マスタ解決済みのmapObjectだけが選定対象になるため、実マスタの要素を載せる
            // Only a map object with a resolved master is selectable, so put a real master element on it
            TestReflection.SetField(mapObject, "<MapObjectMasterElement>k__BackingField", MasterHolder.MapObjectMaster.GetMapObjectElement(TreeMapObjectGuid));

            TargetObjects.Add(targetObject);
            Physics.SyncTransforms();
            return mapObject;
        }

        // CargoCar.prefabの構造を模した車両を作る。当たり判定はMeshRendererを持たないBlockレイヤのCollision子だけが持つ
        // Builds a car mimicking CargoCar.prefab: only the renderer-less Block-layer Collision child carries a collider
        protected TrainCarInteractable CreateTrainCarTarget(Vector3 position)
        {
            var carObject = new GameObject("CargoCar");
            carObject.transform.position = position;
            carObject.AddComponent<Rigidbody>();

            var entityObject = carObject.AddComponent<TrainCarEntityObject>();
            entityObject.Initialize(TrainCarInstanceId.Create(), null);
            var interactable = carObject.AddComponent<TrainCarInteractable>();
            interactable.Initialize(entityObject);

            // メッシュ子はDefaultレイヤなのでレイにも近傍探索にも掛からない
            // Mesh children sit on the Default layer, so neither the ray nor the nearby search ever sees them
            var meshChild = new GameObject("Mesh");
            meshChild.transform.SetParent(carObject.transform, false);
            meshChild.AddComponent<MeshRenderer>();

            var collisionChild = new GameObject("Collision") { layer = LayerConst.BlockLayer };
            collisionChild.transform.SetParent(carObject.transform, false);
            collisionChild.AddComponent<BoxCollider>().size = Vector3.one * 0.1f;

            TargetObjects.Add(carObject);
            Physics.SyncTransforms();
            return interactable;
        }
    }
}
