using System.Collections.Generic;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Unit;
using NUnit.Framework;
using Server.Util.MessagePack;

namespace Client.Tests
{
    public class TrainUnitFutureMessageBufferTest
    {
        private TrainUnitTickState _tickState;
        private TrainUnitFutureMessageBuffer _buffer;

        [SetUp]
        public void SetUp()
        {
            // バッファ検証に必要な最小依存だけを組み立てる。
            // Build only the minimum dependencies required for buffer tests.
            _tickState = new TrainUnitTickState();
            _buffer = new TrainUnitFutureMessageBuffer(_tickState);
        }

        [Test]
        public void FlushPreBySimulatedTick_AppliesOnlyPreEvents()
        {
            var applied = new List<string>();
            _tickState.SetSnapshotBaselineTick(10);
            _buffer.EnqueuePre(11, TrainTickBufferedEvent.Create("preA", () => applied.Add("preA")));
            _buffer.EnqueuePost(11, TrainTickBufferedEvent.Create("postA", () => applied.Add("postA")));

            _tickState.AdvanceTick();
            _buffer.FlushPreBySimulatedTick();

            CollectionAssert.AreEqual(new[] { "preA" }, applied);
        }

        [Test]
        public void FlushPostBySimulatedTick_AppliesOnlyPostEvents()
        {
            var applied = new List<string>();
            _tickState.SetSnapshotBaselineTick(20);
            _buffer.EnqueuePre(21, TrainTickBufferedEvent.Create("preA", () => applied.Add("preA")));
            _buffer.EnqueuePost(21, TrainTickBufferedEvent.Create("postA", () => applied.Add("postA")));

            _tickState.AdvanceTick();
            _buffer.FlushPostBySimulatedTick();

            CollectionAssert.AreEqual(new[] { "postA" }, applied);
        }

        [Test]
        public void EnqueueHash_AcceptsOnlyFutureTickAndTracksReceivedTick()
        {
            // 現在tick以下のhashは捨て、未来tickのみをキューへ入れる。
            // Ignore hash at or before current tick, and enqueue only future hash.
            _tickState.SetSnapshotBaselineTick(100);

            _buffer.EnqueueHash(CreateHashMessage(10, 99));
            Assert.AreEqual(100, _tickState.GetHashReceivedTick());
            Assert.IsFalse(_buffer.TryDequeueHashAtTick(99, out _));

            _buffer.EnqueueHash(CreateHashMessage(20, 101));
            Assert.AreEqual(101, _tickState.GetHashReceivedTick());
            Assert.IsTrue(_buffer.TryDequeueHashAtTick(101, out var message));
            Assert.AreEqual((uint)20, message.UnitsHash);
            Assert.AreEqual(101, message.TrainTick);
            Assert.IsFalse(_buffer.TryDequeueHashAtTick(101, out _));

            #region Internal

            TrainUnitHashStateMessagePack CreateHashMessage(uint unitsHash, long trainTick)
            {
                // テスト用のhashイベントを明示的に作る。
                // Build a typed hash event for test scenarios.
                return new TrainUnitHashStateMessagePack(unitsHash, trainTick);
            }

            #endregion
        }
        
        [Test]
        public void DiscardUpToTick_RemovesQueuedHashesAtOrBelowTick()
        {
            // スナップショット適用後は対象tick以下のキューを破棄する。
            // Discard queued hash entries up to the snapshot-covered tick.
            _tickState.SetSnapshotBaselineTick(10);
            _buffer.EnqueueHash(CreateHashMessage(10, 11));
            _buffer.EnqueueHash(CreateHashMessage(20, 12));
            _buffer.EnqueueHash(CreateHashMessage(30, 13));

            _buffer.DiscardUpToTick(12);

            Assert.IsFalse(_buffer.TryDequeueHashAtTick(11, out _));
            Assert.IsFalse(_buffer.TryDequeueHashAtTick(12, out _));
            Assert.IsTrue(_buffer.TryDequeueHashAtTick(13, out var message));
            Assert.AreEqual((uint)30, message.UnitsHash);

            #region Internal

            TrainUnitHashStateMessagePack CreateHashMessage(uint unitsHash, long trainTick)
            {
                // テスト用のhashイベントを明示的に作る。
                // Build a typed hash event for test scenarios.
                return new TrainUnitHashStateMessagePack(unitsHash, trainTick);
            }

            #endregion
        }
    }
}
