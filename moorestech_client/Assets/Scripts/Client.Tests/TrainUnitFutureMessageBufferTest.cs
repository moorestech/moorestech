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
            _tickState.SetSnapshotBaseline(10, 100);
            _buffer.EnqueuePre(11, 101, TrainTickBufferedEvent.Create("preA", () => applied.Add("preA")));
            _buffer.EnqueuePost(11, 102, TrainTickBufferedEvent.Create("postA", () => applied.Add("postA")));

            _tickState.AdvanceTick();
            _buffer.FlushPreBySimulatedTick();

            CollectionAssert.AreEqual(new[] { "preA" }, applied);
        }

        [Test]
        public void FlushPostBySimulatedTick_AppliesOnlyPostEvents()
        {
            var applied = new List<string>();
            _tickState.SetSnapshotBaseline(20, 200);
            _buffer.EnqueuePre(21, 201, TrainTickBufferedEvent.Create("preA", () => applied.Add("preA")));
            _buffer.EnqueuePost(21, 202, TrainTickBufferedEvent.Create("postA", () => applied.Add("postA")));

            _tickState.AdvanceTick();
            _buffer.FlushPostBySimulatedTick();

            CollectionAssert.AreEqual(new[] { "postA" }, applied);
        }

        [Test]
        public void RecordSimulationRequest_ConsumeResetsCount()
        {
            _buffer.RecordSimulationRequest();
            _buffer.RecordSimulationRequest();

            Assert.AreEqual(2, _buffer.ConsumeSimulationRequestCount());
            Assert.AreEqual(0, _buffer.ConsumeSimulationRequestCount());
        }

        [Test]
        public void EnqueuePre_AllowsCurrentTickFutureSequence()
        {
            // 同一tickでも未適用sequenceなら受け入れて適用できることを確認する。
            // Ensure same-tick future sequence events are accepted and applied.
            var applied = new List<string>();
            _tickState.SetSnapshotBaseline(50, 500);
            _buffer.EnqueuePre(50, 501, TrainTickBufferedEvent.Create("preCurrentTick", () => applied.Add("preCurrentTick")));

            _buffer.FlushPreBySimulatedTick();

            CollectionAssert.AreEqual(new[] { "preCurrentTick" }, applied);
        }

        [Test]
        public void EnqueueHash_AcceptsOnlyFutureTickAndTracksReceivedTick()
        {
            // 現在tick以下のhashは捨て、未来tickのみをキューへ入れる。
            // Ignore hash at or before current tick, and enqueue only future hash.
            _tickState.SetSnapshotBaseline(100, 1000);

            _buffer.EnqueueHash(CreateHashMessage(10, 100, 99, 1001));
            Assert.AreEqual(100, _tickState.GetHashReceivedTick());
            Assert.IsFalse(_buffer.TryDequeueHashAtTick(99, out _));

            _buffer.EnqueueHash(CreateHashMessage(20, 200, 101, 1002));
            Assert.AreEqual(101, _tickState.GetHashReceivedTick());
            Assert.IsTrue(_buffer.TryDequeueHashAtTick(101, out var message));
            Assert.AreEqual((uint)20, message.UnitsHash);
            Assert.AreEqual((uint)200, message.RailGraphHash);
            Assert.AreEqual(101, message.ServerTick);
            Assert.AreEqual((uint)1002, message.TickSequenceId);
            Assert.IsFalse(_buffer.TryDequeueHashAtTick(101, out _));

            #region Internal

            TrainUnitHashStateMessagePack CreateHashMessage(uint unitsHash, uint railGraphHash, uint serverTick, uint tickSequenceId)
            {
                // テスト用のhashイベントを明示的に作る。
                // Build a typed hash event for test scenarios.
                return new TrainUnitHashStateMessagePack(unitsHash, railGraphHash, serverTick, tickSequenceId);
            }

            #endregion
        }
        
        [Test]
        public void DiscardUpToTickUnifiedId_RemovesQueuedHashesAtOrBelowTick()
        {
            // スナップショット適用後は対象tick以下のキューを破棄する。
            // Discard queued hash entries up to the snapshot-covered tick.
            _tickState.SetSnapshotBaseline(10, 110);
            _buffer.EnqueueHash(CreateHashMessage(10, 100, 11, 111));
            _buffer.EnqueueHash(CreateHashMessage(20, 200, 12, 112));
            _buffer.EnqueueHash(CreateHashMessage(30, 300, 13, 113));

            _buffer.DiscardUpToTickUnifiedId(TrainTickUnifiedIdUtility.CreateTickUnifiedId(12, uint.MaxValue));

            Assert.IsFalse(_buffer.TryDequeueHashAtTick(11, out _));
            Assert.IsFalse(_buffer.TryDequeueHashAtTick(12, out _));
            Assert.IsTrue(_buffer.TryDequeueHashAtTick(13, out var message));
            Assert.AreEqual((uint)30, message.UnitsHash);
            Assert.AreEqual((uint)300, message.RailGraphHash);
            Assert.AreEqual((uint)113, message.TickSequenceId);

            #region Internal

            TrainUnitHashStateMessagePack CreateHashMessage(uint unitsHash, uint railGraphHash, uint serverTick, uint tickSequenceId)
            {
                // テスト用のhashイベントを明示的に作る。
                // Build a typed hash event for test scenarios.
                return new TrainUnitHashStateMessagePack(unitsHash, railGraphHash, serverTick, tickSequenceId);
            }

            #endregion
        }

        [Test]
        public void DiscardUpToTickUnifiedId_RemovesQueuedEventsAndHashesAtOrBelowSequence()
        {
            // スナップショット基準sequence以下のイベントとhashが破棄されることを確認する。
            // Ensure events/hashes at or below snapshot sequence baseline are discarded.
            var applied = new List<string>();
            _tickState.SetSnapshotBaseline(50, 500);

            _buffer.EnqueuePre(51, 501, TrainTickBufferedEvent.Create("preA", () => applied.Add("preA")));
            _buffer.EnqueuePre(51, 502, TrainTickBufferedEvent.Create("preB", () => applied.Add("preB")));
            _buffer.EnqueueHash(CreateHashMessage(11, 22, 51, 501));
            _buffer.EnqueueHash(CreateHashMessage(33, 44, 52, 503));

            _buffer.DiscardUpToTickUnifiedId(TrainTickUnifiedIdUtility.CreateTickUnifiedId(51, 501));

            _tickState.AdvanceTick();
            _buffer.FlushPreBySimulatedTick();
            CollectionAssert.AreEqual(new[] { "preB" }, applied);
            Assert.IsFalse(_buffer.TryDequeueHashAtTick(51, out _));
            Assert.IsTrue(_buffer.TryDequeueHashAtTick(52, out var hash));
            Assert.AreEqual((uint)503, hash.TickSequenceId);

            #region Internal

            TrainUnitHashStateMessagePack CreateHashMessage(uint unitsHash, uint railGraphHash, uint serverTick, uint tickSequenceId)
            {
                // テスト用のhashイベントを明示的に作る。
                // Build a typed hash event for test scenarios.
                return new TrainUnitHashStateMessagePack(unitsHash, railGraphHash, serverTick, tickSequenceId);
            }

            #endregion
        }
    }
}
