using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.BuildUndo
{
    public class BlockFootprintOccupancyTest
    {
        private GameObject _storeObject;
        private GameObject _blockObject;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [TearDown]
        public void TearDown()
        {
            if (_blockObject != null) Object.DestroyImmediate(_blockObject);
            if (_storeObject != null) Object.DestroyImmediate(_storeObject);
        }

        [Test]
        public void QueryDistinguishesSameOriginFromOverlappingOtherFootprint()
        {
            _storeObject = new GameObject("OccupancyStore");
            _blockObject = new GameObject("PlacedBlock");
            var store = _storeObject.AddComponent<BlockGameObjectDataStore>();
            var block = _blockObject.AddComponent<BlockGameObject>();
            var blockId = ForUnitTestModBlockId.MachineId;
            var footprint = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, new Vector3Int(2, 1, 2));
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockId)).GetSetMethod(true).Invoke(block, new object[] { blockId });
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockPosInfo)).GetSetMethod(true).Invoke(block, new object[] { footprint });
            var blocks = (Dictionary<Vector3Int, BlockGameObject>)store.BlockGameObjectDictionary;
            blocks.Add(Vector3Int.zero, block);

            // 辞書キー一致と占有範囲の重なりを別の結果にする
            // Distinguish an exact origin match from overlapping footprints
            Assert.AreEqual(BlockFootprintOccupancy.SameBlockPresent, store.GetOccupancy(Vector3Int.zero, BlockDirection.North, blockId));
            Assert.AreEqual(BlockFootprintOccupancy.OtherBlock,
                store.GetOccupancy(Vector3Int.right, BlockDirection.North, blockId));
            Assert.AreEqual(BlockFootprintOccupancy.Free,
                store.GetOccupancy(new Vector3Int(8, 0, 0), BlockDirection.North, blockId));

        }

        [Test]
        public void QueryUsesMasterSizeForMultiCellCandidate()
        {
            _storeObject = new GameObject("OccupancyStore");
            _blockObject = new GameObject("PlacedBlock");
            var store = _storeObject.AddComponent<BlockGameObjectDataStore>();
            var block = _blockObject.AddComponent<BlockGameObject>();
            var existingPosition = new Vector3Int(2, 0, 0);
            var existingId = ForUnitTestModBlockId.MachineId;
            var existingFootprint = new BlockPositionInfo(existingPosition, BlockDirection.North, Vector3Int.one);
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockId)).GetSetMethod(true).Invoke(block, new object[] { existingId });
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockPosInfo)).GetSetMethod(true).Invoke(block, new object[] { existingFootprint });
            var blocks = (Dictionary<Vector3Int, BlockGameObject>)store.BlockGameObjectDictionary;
            blocks.Add(existingPosition, block);

            // MultiBlock の寸法をマスタから読み、離れた原点の重なりを見つける
            // Read the multi-block size from master data and find overlap across origins
            Assert.AreEqual(BlockFootprintOccupancy.OtherBlock,
                store.GetOccupancy(Vector3Int.zero, BlockDirection.North, ForUnitTestModBlockId.MultiBlockGeneratorId));
        }
    }
}
