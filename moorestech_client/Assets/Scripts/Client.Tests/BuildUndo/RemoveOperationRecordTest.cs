using UnityEngine.TestTools;
using Cysharp.Threading.Tasks;
using Server.Boot;
using Tests.Module.TestMod;
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.UI.UIState.State;
using Client.Tests.UIState.Fakes;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BuildUndo
{
    /// <summary>
    ///     撤去Undoの重複排除とブロック→線の順を検証
    ///     Verifies undo dedupes removals and restores blocks first, then lines
    /// </summary>
    public class RemoveOperationRecordTest
    {
        [SetUp]
        public void SetUp()
        {
            // 実在テスト用ブロックの寸法で占有判定
            // Judge occupancy with a real test block's footprint
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void BlocksAreRestoredBeforeLinesAndDuplicatesCollapse()
        {
            // ブロック撤去の巻き込み線と直接選択した線が同じ線なら1回だけ引き直す
            // A line both cascaded from a block and selected directly is restored once
            var guid = Guid.NewGuid();
            var posA = new Vector3Int(0, 0, 0);
            var posB = new Vector3Int(5, 0, 0);
            var commands = new FakeConnectionLineCommands(ConnectionLineKind.ElectricWire);
            var line = new RemovedConnectionLine(posA, posB, guid, commands);
            var sameLineReversed = new RemovedConnectionLine(posB, posA, guid, commands);
            var block = new RemovedBlock(posA, ForUnitTestModBlockId.MachineId, BlockDirection.North, Array.Empty<BlockCreateParam>());
            var targets = new List<IDeleteTarget>
            {
                new FakeDeleteTarget { RemovedObjects = { line } },
                new FakeDeleteTarget { RemovedObjects = { block, sameLineReversed } },
            };
            var sender = new FakeRemovalRestoreSender();

            var record = RemoveOperationRecord.CreateFrom(targets, sender);
            record.UndoAsync(new FakeOccupancy(false)).GetAwaiter().GetResult();

            Assert.IsTrue(record.HasRemovedObjects);
            CollectionAssert.AreEqual(new[] { "place:1", $"wire:{posA}-{posB}:{guid}" }, sender.Sent);
        }

        [Test]
        public void OccupiedBlockIsSkippedButLinesAreStillSent()
        {
            // 占有済みは再設置せず線だけ送る
            // An occupied cell is skipped, but the line restore is sent
            var guid = Guid.NewGuid();
            var block = new RemovedBlock(Vector3Int.zero, ForUnitTestModBlockId.MachineId, BlockDirection.North, Array.Empty<BlockCreateParam>());
            var chain = new RemovedConnectionLine(Vector3Int.zero, new Vector3Int(3, 0, 0), guid, new FakeConnectionLineCommands(ConnectionLineKind.GearChain));
            var sender = new FakeRemovalRestoreSender();
            var record = RemoveOperationRecord.CreateFrom(new List<IDeleteTarget> { new FakeDeleteTarget { RemovedObjects = { block, chain } } }, sender);

            LogAssert.Expect(LogType.Warning, $"[RemovalRestore] skip re-place: footprint occupied at {Vector3Int.zero}");
            record.UndoAsync(new FakeOccupancy(true)).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { $"chain:{Vector3Int.zero}-{new Vector3Int(3, 0, 0)}:{guid}", "skipped:1" }, sender.Sent);
        }

        [TestCase(ConnectionLineKind.ElectricWire)]
        [TestCase(ConnectionLineKind.GearChain)]
        public void RestoreRequestIsAlwaysSentToServer(ConnectionLineKind kind)
        {
            // 既接続の判定はサーバーに任せる
            // Let the server decide whether the line is already connected
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(3, 0, 0);
            var tool = Guid.NewGuid();
            var line = new RemovedConnectionLine(posA, posB, tool, new FakeConnectionLineCommands(kind));
            var sender = new FakeRemovalRestoreSender();
            var record = RemoveOperationRecord.CreateFrom(new List<IDeleteTarget> { new FakeDeleteTarget { RemovedObjects = { line } } }, sender);

            record.UndoAsync(new FakeOccupancy(false)).GetAwaiter().GetResult();

            var prefix = kind == ConnectionLineKind.ElectricWire ? "wire" : "chain";
            CollectionAssert.AreEqual(new[] { $"{prefix}:{posA}-{posB}:{tool}" }, sender.Sent);
        }

        [Test]
        public void ConnectionOfAnotherKindDoesNotSuppressRestore()
        {
            // チェーンがあっても電線の復元は送る
            // A chain at the same endpoints does not suppress a wire restore
            var posA = Vector3Int.zero;
            var posB = Vector3Int.right;
            var tool = Guid.NewGuid();
            var line = new RemovedConnectionLine(posA, posB, tool, new FakeConnectionLineCommands(ConnectionLineKind.ElectricWire));
            var sender = new FakeRemovalRestoreSender();
            var record = RemoveOperationRecord.CreateFrom(new List<IDeleteTarget> { new FakeDeleteTarget { RemovedObjects = { line } } }, sender);

            record.UndoAsync(new FakeOccupancy(false)).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { $"wire:{posA}-{posB}:{tool}" }, sender.Sent);
        }

        [Test]
        public void UnrecordableObjectsAreNotifiedOnUndo()
        {
            // 記録不能だった物も件数で通知する
            // Unrecordable objects are also reported by count on undo
            LogAssert.Expect(LogType.Warning, "[RemovalRestore] unrecordable: rail node not synced");
            var target = new FakeDeleteTarget { UnrecordableReasons = { "rail node not synced" } };
            var sender = new FakeRemovalRestoreSender();
            var record = RemoveOperationRecord.CreateFrom(new List<IDeleteTarget> { target }, sender);

            Assert.IsTrue(record.HasRemovedObjects);
            record.UndoAsync(new FakeOccupancy(false)).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "skipped:1" }, sender.Sent);
        }

        [Test]
        public void SameBlockStillPresentIsNotCountedAsSkipped()
        {
            var block = new RemovedBlock(Vector3Int.zero, ForUnitTestModBlockId.MachineId, BlockDirection.North, Array.Empty<BlockCreateParam>());
            var sender = new FakeRemovalRestoreSender();
            var record = RemoveOperationRecord.CreateFrom(new[] { new FakeDeleteTarget { RemovedObjects = { block } } }, sender);

            record.UndoAsync(new FakeOccupancy(BlockFootprintOccupancy.SameBlockPresent)).GetAwaiter().GetResult();
            Assert.IsEmpty(sender.Sent);
        }

        [TestCase(BlockFootprintOccupancy.SameBlockPresent, 0)]
        [TestCase(BlockFootprintOccupancy.Free, 1)]
        public void UnrecordableBlockCountsOnlyAfterItHasGone(BlockFootprintOccupancy occupancy, int expectedSkipped)
        {
            var target = new FakeDeleteTarget();
            target.UnrecordableBlocks.Add((Vector3Int.zero, BlockDirection.North, ForUnitTestModBlockId.MachineId, "missing create params"));
            var sender = new FakeRemovalRestoreSender();
            LogAssert.Expect(LogType.Warning, "[RemovalRestore] unrecordable: missing create params");
            var record = RemoveOperationRecord.CreateFrom(new[] { target }, sender);

            record.UndoAsync(new FakeOccupancy(occupancy)).GetAwaiter().GetResult();
            if (expectedSkipped == 0) Assert.IsEmpty(sender.Sent);
            else CollectionAssert.AreEqual(new[] { "skipped:1" }, sender.Sent);
        }

        [Test]
        public void TargetsWithoutRemovedObjectsYieldEmptyRecord()
        {
            // 何も記録しない対象だけなら履歴に積まない
            // Targets recording nothing produce no history entry
            var record = RemoveOperationRecord.CreateFrom(new List<IDeleteTarget> { new FakeDeleteTarget() }, new FakeRemovalRestoreSender());
            Assert.IsFalse(record.HasRemovedObjects);
        }

        private class FakeOccupancy : IBlockOccupancyQuery
        {
            private readonly BlockFootprintOccupancy _occupancy;
            public FakeOccupancy(bool occupied)
            {
                _occupancy = occupied ? BlockFootprintOccupancy.OtherBlock : BlockFootprintOccupancy.Free;
            }
            public FakeOccupancy(BlockFootprintOccupancy occupancy) { _occupancy = occupancy; }
            public BlockFootprintOccupancy GetOccupancy(Vector3Int origin, BlockDirection direction, BlockId blockId)
            {
                return _occupancy;
            }
        }
    }
}
