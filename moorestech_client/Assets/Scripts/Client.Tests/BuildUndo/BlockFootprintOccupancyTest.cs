using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BuildUndo
{
    public class BlockFootprintOccupancyTest
    {
        private GameObject _storeObject;
        private GameObject _blockObject;

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
            var blockId = new BlockId(1);
            var footprint = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, new Vector3Int(2, 1, 2));
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockId)).GetSetMethod(true).Invoke(block, new object[] { blockId });
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockPosInfo)).GetSetMethod(true).Invoke(block, new object[] { footprint });
            var blocks = (Dictionary<Vector3Int, BlockGameObject>)store.BlockGameObjectDictionary;
            blocks.Add(Vector3Int.zero, block);

            // 辞書キー一致と占有範囲の重なりを別の結果にする
            // Distinguish an exact origin match from overlapping footprints
            Assert.AreEqual(BlockFootprintOccupancy.SameBlockPresent, store.GetOccupancy(footprint, blockId));
            Assert.AreEqual(BlockFootprintOccupancy.OtherBlock,
                store.GetOccupancy(new BlockPositionInfo(Vector3Int.right, BlockDirection.North, Vector3Int.one), blockId));
            Assert.AreEqual(BlockFootprintOccupancy.Free,
                store.GetOccupancy(new BlockPositionInfo(new Vector3Int(8, 0, 0), BlockDirection.North, Vector3Int.one), blockId));

        }
    }
}
