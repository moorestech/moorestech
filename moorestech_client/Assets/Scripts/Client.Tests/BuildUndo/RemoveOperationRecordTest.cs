using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Tests.BuildUndo
{
    public class RemoveOperationRecordTest
    {
        [Test]
        public void SelectReplaceableCellsは占有されていないセルだけを返す()
        {
            var record = new RemoveOperationRecord(new List<RemovedBlockInfo>
            {
                new(new Vector3Int(0, 0, 0), new BlockId(1), BlockDirection.North), // 空 → 再設置対象
                new(new Vector3Int(1, 0, 0), new BlockId(1), BlockDirection.East),  // 占有（撤去失敗 or 他者設置）→ 除外
            });

            var occupied = new HashSet<Vector3Int> { new(1, 0, 0) };
            var cells = record.SelectReplaceableCells(info => occupied.Contains(info.Position));

            Assert.AreEqual(1, cells.Count);
            Assert.AreEqual(new Vector3Int(0, 0, 0), cells[0].Position);
            Assert.AreEqual(BlockDirection.North, cells[0].Direction);
        }
    }
}
