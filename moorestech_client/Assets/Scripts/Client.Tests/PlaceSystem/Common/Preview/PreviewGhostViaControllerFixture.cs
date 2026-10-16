#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.Context;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Common.Preview
{
    /// <summary>
    ///     本番入口でゴーストを作る土台。MasterHolderは呼び出し側が用意
    ///     Builds ghosts through the production entry (controller, pool, PreviewBlockCreator). The caller must have loaded MasterHolder
    /// </summary>
    internal sealed class PreviewGhostViaControllerFixture
    {
        private GameObject _prefab;
        private GameObject _controllerObject;
        private Material _material;
        private BlockGameObjectPrefabContainer _previousContainer;
        private PlacementPreviewBlockGameObjectController _controller;

        internal GameObject DormantChild { get; private set; }

        internal void Initialize()
        {
            // 非表示の子と複数Colliderの最小Prefabを登録
            // Register a minimal prefab with a dormant child holding several colliders
            var blockId = ForUnitTestModBlockId.BlockId;
            _prefab = new GameObject("PreviewGhostSourcePrefab");
            _material = new Material(Shader.Find("Sprites/Default"));
            _prefab.AddComponent<MeshRenderer>().sharedMaterial = _material;
            _prefab.layer = LayerConst.BlockLayer;
            _prefab.AddComponent<BoxCollider>();
            DormantChild = new GameObject("DormantColliderChild") { layer = LayerConst.BlockLayer };
            DormantChild.transform.SetParent(_prefab.transform, false);
            DormantChild.AddComponent<BoxCollider>();
            DormantChild.AddComponent<SphereCollider>();
            DormantChild.SetActive(false);
            _prefab.SetActive(false);

            var info = new BlockPrefabInfo(blockId, _prefab, MasterHolder.BlockMaster.GetBlockMaster(blockId));
            _previousContainer = ClientContext.BlockGameObjectPrefabContainer;
            SetContainer(new BlockGameObjectPrefabContainer(null, new Dictionary<BlockId, BlockPrefabInfo> { { blockId, info } }));

            _controllerObject = new GameObject("PreviewGhostController");
            _controller = _controllerObject.AddComponent<PlacementPreviewBlockGameObjectController>();
            // EditModeではAwakeが走らないので、本番のプール初期化を明示的に呼ぶ
            // Awake does not run in EditMode, so invoke the production pool initialization explicitly
            typeof(PlacementPreviewBlockGameObjectController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_controller, null);
            _controller.SetActive(true);
        }

        internal BlockPreviewObject SetPreview()
        {
            var blockId = ForUnitTestModBlockId.BlockId;
            var placeInfo = new PlaceInfo { BlockId = blockId, Position = Vector3Int.zero, Direction = BlockDirection.North, Placeable = true };
            _controller.SetPreview(new List<PlaceInfo> { placeInfo }, MasterHolder.BlockMaster.GetBlockMaster(blockId));
            Assert.IsTrue(_controller.TryGetPreviewBlock(0, out var preview));
            return preview;
        }

        internal void Dispose()
        {
            SetContainer(_previousContainer);
            foreach (var renderer in _controllerObject.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials) Object.DestroyImmediate(material);
            Object.DestroyImmediate(_controllerObject);
            Object.DestroyImmediate(_prefab);
            Object.DestroyImmediate(_material);
        }

        private static void SetContainer(BlockGameObjectPrefabContainer container)
        {
            typeof(ClientContext).GetProperty(nameof(ClientContext.BlockGameObjectPrefabContainer))
                .GetSetMethod(true).Invoke(null, new object[] { container });
        }
    }
}
#endif
