using System;
using System.Collections.Generic;
using System.Linq;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.Interact;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Player;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Tests.Common;
using Core.Master;
using Game.Block.Interface;
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
        private const string OpenableBlockName = "TestElectricMachine";

        private readonly List<GameObject> _previousMainCameraObjects = new();
        protected readonly List<GameObject> TargetObjects = new();

        // 開いたインベントリはIDから今の表示を引き直すので、テストでも登録簿を通す
        // An opened inventory re-resolves its view by ID, so tests go through the registries too
        protected readonly TrainCarViewRegistryFake TrainCarViewRegistry = new();
        private GameObject _blockDataStoreObject;
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
            CreateBlockDataStore();
            CreateCamera();
            CreateEventSystem();
            CreatePlayerSystem();

            #region Internal

            void CreateBlockDataStore()
            {
                // BlockOpenInteractActionが開いた位置から表示を引き直す先なので、空の実datastoreを立てる
                // BlockOpenInteractAction re-resolves the view by position from here, so stand up an empty real datastore
                _blockDataStoreObject = new GameObject("BlockGameObjectDataStore");
                ClientDIContext.BlockGameObjectDataStore = _blockDataStoreObject.AddComponent<BlockGameObjectDataStore>();
            }

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
            UnityEngine.Object.DestroyImmediate(_blockDataStoreObject);
            ClientDIContext.BlockGameObjectDataStore = null;

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
            targetObject.AddComponent<MapObjectRayTarget>().Initialize(mapObject, interactable: true);

            // マスタ解決済みのmapObjectだけが選定対象になるため、実マスタの要素を載せる
            // Only a map object with a resolved master is selectable, so put a real master element on it
            TestReflection.SetField(mapObject, "<MapObjectMasterElement>k__BackingField", MasterHolder.MapObjectMaster.GetMapObjectElement(TreeMapObjectGuid));

            TargetObjects.Add(targetObject);
            Physics.SyncTransforms();
            return mapObject;
        }

        // BlockGameObject.Initializeはサーバ接続を伴うため、マスタだけ差し込んで面と当たり判定子を直接組む
        // BlockGameObject.Initialize talks to the server, so only the master is injected and the face and hit child are wired directly
        protected BlockInteractable CreateOpenableBlockTarget(Vector3 position)
        {
            var blockObject = new GameObject(OpenableBlockName);
            blockObject.transform.position = position;
            var blockGameObject = blockObject.AddComponent<BlockGameObject>();
            var master = MasterHolder.BlockMaster.Blocks.Data.First(block => block.Name == OpenableBlockName);
            TestReflection.SetField(blockGameObject, "<BlockMasterElement>k__BackingField", master);

            // 位置が索引の鍵になるので、同じテスト内の複数ブロックが別の鍵を持つようにする
            // The position is the index key, so several blocks in one test must not share it
            var originalPos = Vector3Int.RoundToInt(position);
            TestReflection.SetField(blockGameObject, "<BlockPosInfo>k__BackingField", new BlockPositionInfo(originalPos, BlockDirection.North, Vector3Int.one));
            TestReflection.GetField<Dictionary<Vector3Int, BlockGameObject>>(ClientDIContext.BlockGameObjectDataStore, "_blockObjectsDictionary")[originalPos] = blockGameObject;

            var interactable = blockObject.AddComponent<BlockInteractable>();
            interactable.Initialize(blockGameObject);
            TestReflection.SetField(blockGameObject, "<Interactable>k__BackingField", interactable);

            // 当たり判定はBlockレイヤのメッシュ子だけが持ち、そこから面へ案内される
            // Only the Block-layer mesh child carries a collider and points at the face from there
            var meshChild = new GameObject("BlockMesh") { layer = LayerConst.BlockLayer };
            meshChild.transform.SetParent(blockObject.transform, false);
            meshChild.AddComponent<SphereCollider>().radius = 0.05f;
            meshChild.AddComponent<BlockGameObjectChild>().Init(blockGameObject);

            TargetObjects.Add(blockObject);
            Physics.SyncTransforms();
            return interactable;
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
            TrainCarViewRegistry.Register(entityObject);
            var interactable = carObject.AddComponent<TrainCarInteractable>();
            interactable.Initialize(entityObject, TrainCarViewRegistry);
            entityObject.SetInteractable(interactable);

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
