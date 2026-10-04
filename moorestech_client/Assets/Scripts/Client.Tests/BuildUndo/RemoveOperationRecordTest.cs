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
    ///     撤去Undoが撤去物を重複排除し、ブロック→線の順に復元送信することを検証する
    ///     Verifies the removal undo dedupes removed objects and sends restores blocks-first, then lines
    /// </summary>
    public class RemoveOperationRecordTest
    {
        [SetUp]
        public void SetUp()
        {
            // 実在するテスト用ブロックの寸法で占有を判定する
            // Evaluate occupancy using an actual test-mod block footprint
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
            var currentState = new FakeConnectionLineCurrentState();
            var line = new RemovedConnectionLine(ConnectionLineKind.ElectricWire, posA, posB, guid, currentState);
            var sameLineReversed = new RemovedConnectionLine(ConnectionLineKind.ElectricWire, posB, posA, guid, currentState);
            var block = new RemovedBlock(posA, ForUnitTestModBlockId.MachineId, BlockDirection.North);
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
            // 占有済みセルは再設置しないが、線の引き直しは送る（サーバーが端点不在を判定する）
            // An occupied cell is not re-placed, but the line restore is still sent (the server judges missing endpoints)
            var guid = Guid.NewGuid();
            var block = new RemovedBlock(Vector3Int.zero, ForUnitTestModBlockId.MachineId, BlockDirection.North);
            var chain = new RemovedConnectionLine(ConnectionLineKind.GearChain, Vector3Int.zero, new Vector3Int(3, 0, 0), guid, new FakeConnectionLineCurrentState());
            var sender = new FakeRemovalRestoreSender();
            var record = RemoveOperationRecord.CreateFrom(new List<IDeleteTarget> { new FakeDeleteTarget { RemovedObjects = { block, chain } } }, sender);

            LogAssert.Expect(LogType.Warning, $"[RemovalRestore] skip re-place: footprint occupied at {Vector3Int.zero}");
            record.UndoAsync(new FakeOccupancy(true)).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { $"chain:{Vector3Int.zero}-{new Vector3Int(3, 0, 0)}:{guid}", "skipped:1" }, sender.Sent);
        }

        [TestCase(ConnectionLineKind.ElectricWire)]
        [TestCase(ConnectionLineKind.GearChain)]
        public void ExistingConnectionIsNotSentAgain(ConnectionLineKind kind)
        {
            // 撤去が拒否されて線が残った場合、Undoは既接続へ要求を送らない
            // If removal was denied and the line remains, undo sends no duplicate request
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(3, 0, 0);
            var currentState = new FakeConnectionLineCurrentState();
            currentState.SetConnected(kind, posB, posA);
            var line = new RemovedConnectionLine(kind, posA, posB, Guid.NewGuid(), currentState);
            var sender = new FakeRemovalRestoreSender();
            var record = RemoveOperationRecord.CreateFrom(new List<IDeleteTarget> { new FakeDeleteTarget { RemovedObjects = { line } } }, sender);

            record.UndoAsync(new FakeOccupancy(false)).GetAwaiter().GetResult();

            Assert.IsEmpty(sender.Sent);
        }

        [Test]
        public void ConnectionOfAnotherKindDoesNotSuppressRestore()
        {
            // 同じ端点でもチェーンがあるだけなら電線の復元は送る
            // A chain at the same endpoints does not suppress a wire restore
            var posA = Vector3Int.zero;
            var posB = Vector3Int.right;
            var currentState = new FakeConnectionLineCurrentState();
            currentState.SetConnected(ConnectionLineKind.GearChain, posA, posB);
            var tool = Guid.NewGuid();
            var line = new RemovedConnectionLine(ConnectionLineKind.ElectricWire, posA, posB, tool, currentState);
            var sender = new FakeRemovalRestoreSender();
            var record = RemoveOperationRecord.CreateFrom(new List<IDeleteTarget> { new FakeDeleteTarget { RemovedObjects = { line } } }, sender);

            record.UndoAsync(new FakeOccupancy(false)).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { $"wire:{posA}-{posB}:{tool}" }, sender.Sent);
        }

        [Test]
        public void UnrecordableObjectsAreNotifiedOnUndo()
        {
            // 撤去時に記録できなかった物も、Undo時にプレイヤーへ件数で知らせる
            // Objects that could not be recorded at removal are also reported to the player by count on undo
            LogAssert.Expect(LogType.Warning, "[RemovalRestore] unrecordable: rail node not synced");
            var target = new FakeDeleteTarget { UnrecordableReasons = { "rail node not synced" } };
            var sender = new FakeRemovalRestoreSender();
            var record = RemoveOperationRecord.CreateFrom(new List<IDeleteTarget> { target }, sender);

            Assert.IsTrue(record.HasRemovedObjects);
            record.UndoAsync(new FakeOccupancy(false)).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "skipped:1" }, sender.Sent);
        }

        [Test]
        public void TargetsWithoutRemovedObjectsYieldEmptyRecord()
        {
            // 列車のように何も記録しない対象だけなら履歴に積まない（記録できなかった物も無い）
            // Only targets recording nothing (like trains), with nothing unrecordable either, produce no history entry
            var record = RemoveOperationRecord.CreateFrom(new List<IDeleteTarget> { new FakeDeleteTarget() }, new FakeRemovalRestoreSender());
            Assert.IsFalse(record.HasRemovedObjects);
        }

        private class FakeOccupancy : IBlockOccupancyQuery
        {
            private readonly bool _occupied;
            public FakeOccupancy(bool occupied) { _occupied = occupied; }
            public bool IsOverlapPositionInfo(BlockPositionInfo target) { return _occupied; }
        }
    }
}
