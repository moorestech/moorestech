#if UNITY_EDITOR
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.Context;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Tests.PlaceSystem.Common.Preview
{
    internal sealed class PlacementPreviewPhysicsFixture
    {
        private Scene _scene;
        private PhysicsScene _physics;
        private GameObject _prefab;
        private BlockGameObjectPrefabContainer _previousContainer;
        internal PlacementPreviewBlockGameObjectController Controller { get; private set; }

        internal void Initialize()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _scene = Application.isPlaying
                ? SceneManager.CreateScene("PlacementPreviewPhysics", new CreateSceneParameters(LocalPhysicsMode.Physics3D))
                : EditorSceneManager.NewPreviewScene();
            _physics = _scene.GetPhysicsScene();
            Assert.AreNotEqual(Physics.defaultPhysicsScene, _physics);

            // 未設定のColliderを登録し、本番生成側の設定を検査する
            // Register an unconfigured collider to test production setup.
            _prefab = new GameObject("PreviewSource");
            SceneManager.MoveGameObjectToScene(_prefab, _scene);
            _prefab.AddComponent<BoxCollider>();
            _prefab.SetActive(false);
            var blockId = ForUnitTestModBlockId.BlockId;
            var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var info = new BlockPrefabInfo(blockId, _prefab, master);
            _previousContainer = ClientContext.BlockGameObjectPrefabContainer;
            SetContainer(new BlockGameObjectPrefabContainer(null,
                new Dictionary<BlockId, BlockPrefabInfo> { { blockId, info } }));

            // プールの親を隔離シーンへ置いてから本番controllerを起動する
            // Put the pool parent in the isolated scene before starting the controller.
            var root = new GameObject("PlacementPreviewController");
            SceneManager.MoveGameObjectToScene(root, _scene);
            Controller = root.AddComponent<PlacementPreviewBlockGameObjectController>();
            Controller.SetActive(true);
        }

        internal BlockPreviewObject SetPreview()
        {
            var blockId = ForUnitTestModBlockId.BlockId;
            var cell = new PlaceInfo
            {
                BlockId = blockId, Position = Vector3Int.zero, Direction = BlockDirection.North, Placeable = true
            };
            Controller.SetPreview(new List<PlaceInfo> { cell }, MasterHolder.BlockMaster.GetBlockMaster(blockId));
            Assert.IsTrue(Controller.TryGetPreviewBlock(0, out var preview));
            return preview;
        }

        internal BoxCollider CreateGround()
        {
            var ground = new GameObject("ContactGround");
            SceneManager.MoveGameObjectToScene(ground, _scene);
            ground.layer = LayerConst.GroundLayer;
            ground.AddComponent<GroundGameObject>();
            var box = ground.AddComponent<BoxCollider>();
            box.size = Vector3.one * 0.2f;
            box.transform.position = Vector3.one * 100f;
            return box;
        }

        internal void Step()
        {
            // 手動シミュレーションに先行する実際のリセット処理も実行する
            // Run the real reset callback before manual physics simulation.
            foreach (var detector in Controller.GetComponentsInChildren<GroundCollisionDetector>())
                detector.SendMessage("FixedUpdate");
            Physics.SyncTransforms();
            _physics.Simulate(Time.fixedDeltaTime);
        }

        internal void Dispose()
        {
            SetContainer(_previousContainer);
            foreach (var root in _scene.GetRootGameObjects()) Object.DestroyImmediate(root);
            if (Application.isPlaying) SceneManager.UnloadSceneAsync(_scene);
            else EditorSceneManager.ClosePreviewScene(_scene);
        }

        private static void SetContainer(BlockGameObjectPrefabContainer container)
        {
            typeof(ClientContext).GetProperty(nameof(ClientContext.BlockGameObjectPrefabContainer))
                .GetSetMethod(true).Invoke(null, new object[] { container });
        }
    }
}
#endif
