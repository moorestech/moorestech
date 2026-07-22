using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Tests.BuildUndo
{
    public class PlaceOperationRecordTest
    {
        [Test]
        public void CreateFromはPlaceableなセルだけをスナップショットする()
        {
            var placeInfos = new List<PlaceInfo>
            {
                CreatePlaceInfo(new Vector3Int(0, 0, 0), 1, placeable: true),
                CreatePlaceInfo(new Vector3Int(1, 0, 0), 1, placeable: false),
            };
            var record = PlaceOperationRecord.CreateFrom(placeInfos);

            var cells = record.SelectUndoableCells(_ => new BlockId(1));
            CollectionAssert.AreEqual(new[] { new Vector3Int(0, 0, 0) }, cells);
        }

        [Test]
        public void SelectUndoableCellsは同座標同BlockIdのセルだけを返す()
        {
            var placeInfos = new List<PlaceInfo>
            {
                CreatePlaceInfo(new Vector3Int(0, 0, 0), 1, placeable: true), // 現存・ID一致 → 対象
                CreatePlaceInfo(new Vector3Int(1, 0, 0), 1, placeable: true), // 消滅 → 除外
                CreatePlaceInfo(new Vector3Int(2, 0, 0), 1, placeable: true), // 別ブロックに置換 → 除外
            };
            var record = PlaceOperationRecord.CreateFrom(placeInfos);

            var worldState = new Dictionary<Vector3Int, BlockId?>
            {
                [new Vector3Int(0, 0, 0)] = new BlockId(1),
                [new Vector3Int(1, 0, 0)] = null,
                [new Vector3Int(2, 0, 0)] = new BlockId(99),
            };
            var cells = record.SelectUndoableCells(pos => worldState[pos]);
            CollectionAssert.AreEqual(new[] { new Vector3Int(0, 0, 0) }, cells);
        }

        [Test]
        public void CreateFromは元リストのスナップショットであり後からの変更に影響されない()
        {
            var placeInfo = CreatePlaceInfo(new Vector3Int(0, 0, 0), 1, placeable: true);
            var placeInfos = new List<PlaceInfo> { placeInfo };
            var record = PlaceOperationRecord.CreateFrom(placeInfos);

            // 設置システムはPlaceInfoを使い回すため、記録後の変更が履歴を汚してはならない
            // Placement systems reuse PlaceInfo, so later mutation must not corrupt the record
            placeInfo.Position = new Vector3Int(9, 9, 9);
            var cells = record.SelectUndoableCells(_ => new BlockId(1));
            CollectionAssert.AreEqual(new[] { new Vector3Int(0, 0, 0) }, cells);
        }

        private static PlaceInfo CreatePlaceInfo(Vector3Int position, int blockId, bool placeable)
        {
            return new PlaceInfo
            {
                Position = position,
                Direction = BlockDirection.North,
                VerticalDirection = BlockVerticalDirection.Horizontal,
                BlockId = new BlockId(blockId),
                Placeable = placeable,
            };
        }
    }
}
