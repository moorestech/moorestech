using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Tests.PlaceSystem.Undo
{
    /// <summary>
    /// 張替えの逆張替えレコード（送信前の既設ID控え・不可セル除外・他者変更セルの誤爆防止）を検証する
    /// Verifies the reverse-replace record: capturing existing ids before sending, dropping blocked cells, and not clobbering cells changed by others
    /// </summary>
    public class ReplaceOperationRecordTest
    {
        private static readonly BlockId OldBlock = new(201);
        private static readonly BlockId NewBlock = new(202);
        private static readonly BlockId OtherBlock = new(203);
        private GameObject _dataStoreObject;
        private BlockGameObjectDataStore _dataStore;
        private readonly List<GameObject> _blockObjects = new();

        [SetUp]
        public void SetUp()
        {
            _dataStoreObject = new GameObject("BlockGameObjectDataStore");
            _dataStore = _dataStoreObject.AddComponent<BlockGameObjectDataStore>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var blockObject in _blockObjects) Object.DestroyImmediate(blockObject);
            _blockObjects.Clear();
            Object.DestroyImmediate(_dataStoreObject);
        }

        [Test]
        public void 送信前の既設BlockIdを旧ブロックとして記録し不可セルは含めない()
        {
            Register(new Vector3Int(1, 0, 1), OldBlock, BlockDirection.East);
            Register(new Vector3Int(1, 0, 2), OldBlock, BlockDirection.East);
            var placeInfos = new List<PlaceInfo>
            {
                new() { Position = new Vector3Int(1, 0, 1), Direction = BlockDirection.East, VerticalDirection = BlockVerticalDirection.Up, BlockId = NewBlock, IsReplace = true, Placeable = true },
                new() { Position = new Vector3Int(1, 0, 2), Direction = BlockDirection.East, BlockId = NewBlock, IsReplace = true, Placeable = false },
            };

            var record = ReplaceOperationRecord.CreateFrom(placeInfos, _dataStore);

            // 記録済みでも張替えが未反映（まだ旧ブロックのまま）なら戻す対象にならない
            // A recorded cell that is still the old block has nothing to undo yet
            Assert.IsTrue(record.HasCells);
            Assert.AreEqual(0, record.BuildUndoPlaceInfos(_dataStore).Count);

            SetBlockId(new Vector3Int(1, 0, 1), NewBlock);
            SetBlockId(new Vector3Int(1, 0, 2), NewBlock);
            var undoPlaceInfos = record.BuildUndoPlaceInfos(_dataStore);

            // 不可だったz=2は記録自体に入らないため、張り替わっていても戻さない
            // z=2 was blocked so it never entered the record and stays untouched even after it changes
            Assert.AreEqual(1, undoPlaceInfos.Count);
            Assert.AreEqual(new Vector3Int(1, 0, 1), undoPlaceInfos[0].Position);
            Assert.AreEqual(OldBlock, undoPlaceInfos[0].BlockId);
            Assert.AreEqual(BlockDirection.East, undoPlaceInfos[0].Direction);
            Assert.AreEqual(BlockVerticalDirection.Up, undoPlaceInfos[0].VerticalDirection);
            Assert.IsTrue(undoPlaceInfos[0].IsReplace);
            Assert.IsTrue(undoPlaceInfos[0].Placeable);
        }

        [Test]
        public void 張替え後に別ブロックへ変わったセルは戻さない()
        {
            Register(new Vector3Int(0, 0, 0), OldBlock, BlockDirection.North);
            var placeInfos = new List<PlaceInfo>
            {
                new() { Position = new Vector3Int(0, 0, 0), Direction = BlockDirection.North, BlockId = NewBlock, IsReplace = true, Placeable = true },
            };
            var record = ReplaceOperationRecord.CreateFrom(placeInfos, _dataStore);

            SetBlockId(new Vector3Int(0, 0, 0), OtherBlock);

            Assert.IsEmpty(record.BuildUndoPlaceInfos(_dataStore));
        }

        [Test]
        public void 張替えでないセルは記録しない()
        {
            Register(new Vector3Int(0, 0, 0), OldBlock, BlockDirection.North);
            var placeInfos = new List<PlaceInfo>
            {
                new() { Position = new Vector3Int(0, 0, 0), Direction = BlockDirection.North, BlockId = NewBlock, IsReplace = false, Placeable = true },
            };

            Assert.IsFalse(ReplaceOperationRecord.CreateFrom(placeInfos, _dataStore).HasCells);
        }

        private void SetBlockId(Vector3Int position, BlockId blockId)
        {
            SetBackingField(Dictionary()[position], nameof(BlockGameObject.BlockId), blockId);
        }

        private void Register(Vector3Int position, BlockId blockId, BlockDirection direction)
        {
            var blockObject = new GameObject($"Block_{position}");
            _blockObjects.Add(blockObject);
            var blockGameObject = blockObject.AddComponent<BlockGameObject>();
            SetBackingField(blockGameObject, nameof(BlockGameObject.BlockPosInfo), new BlockPositionInfo(position, direction, Vector3Int.one));
            SetBackingField(blockGameObject, nameof(BlockGameObject.BlockId), blockId);
            Dictionary().Add(position, blockGameObject);
        }

        private Dictionary<Vector3Int, BlockGameObject> Dictionary()
        {
            return (Dictionary<Vector3Int, BlockGameObject>)typeof(BlockGameObjectDataStore)
                .GetField("_blockObjectsDictionary", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_dataStore);
        }

        private static void SetBackingField(BlockGameObject blockGameObject, string propertyName, object value)
        {
            typeof(BlockGameObject)
                .GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(blockGameObject, value);
        }
    }
}
