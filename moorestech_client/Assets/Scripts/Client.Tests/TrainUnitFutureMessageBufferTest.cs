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
        public void TryFlushEvent_AppliesQueuedEventAndUpdatesAppliedTickUnifiedId()
        {
            // 指定した tickUnifiedId のイベントが適用され、状態が進むことを確認する。
            // Ensure queued event is applied and state advances at requested unified id.
            var applied = new List<string>();
            _tickState.RecordAppliedTickUnifiedId(10, 0);
            _buffer.EnqueueEvent(11, 1, TrainTickBufferedEvent.Create(() => applied.Add("eventA")));

            var flushed = _buffer.TryFlushEvent(11, 1);
            var flushedAgain = _buffer.TryFlushEvent(11, 1);

            Assert.IsTrue(flushed);
            Assert.IsFalse(flushedAgain);
            CollectionAssert.AreEqual(new[] { "eventA" }, applied);
            Assert.AreEqual(
                TrainTickUnifiedIdUtility.CreateTickUnifiedId(11, 1),
                _tickState.GetAppliedTickUnifiedId());
        }

        [Test]
        public void EnqueueEvent_DropsStaleEventAtOrBelowAppliedTickUnifiedId()
        {
            // 適用済み以下のイベントは破棄され、未来イベントのみ適用されることを確認する。
            // Ensure stale events are dropped and only future events are applied.
            var applied = new List<string>();
            _tickState.RecordAppliedTickUnifiedId(20, 5);
            _buffer.EnqueueEvent(20, 4, TrainTickBufferedEvent.Create(() => applied.Add("staleA")));
            _buffer.EnqueueEvent(20, 5, TrainTickBufferedEvent.Create(() => applied.Add("staleB")));
            _buffer.EnqueueEvent(21, 0, TrainTickBufferedEvent.Create(() => applied.Add("future")));

            Assert.IsFalse(_buffer.TryFlushEvent(20, 4));
            Assert.IsFalse(_buffer.TryFlushEvent(20, 5));
            Assert.IsTrue(_buffer.TryFlushEvent(21, 0));
            CollectionAssert.AreEqual(new[] { "future" }, applied);
        }

        [Test]
        public void TryFlushEvent_ByUnifiedIdProcessesQueuedEventsInSequence()
        {
            // 同一tick内の sequence 順でイベントが適用できることを確認する。
            // Ensure events can be applied in same-tick sequence order.
            var applied = new List<string>();
            _tickState.RecordAppliedTickUnifiedId(50, 0);
            _buffer.EnqueueEvent(50, 2, TrainTickBufferedEvent.Create(() => applied.Add("event2")));
            _buffer.EnqueueEvent(50, 1, TrainTickBufferedEvent.Create(() => applied.Add("event1")));

            Assert.IsTrue(_buffer.TryFlushEvent(TrainTickUnifiedIdUtility.CreateTickUnifiedId(50, 1)));
            Assert.IsTrue(_buffer.TryFlushEvent(TrainTickUnifiedIdUtility.CreateTickUnifiedId(50, 2)));
            CollectionAssert.AreEqual(new[] { "event1", "event2" }, applied);
        }
        
        [Test]
        public void TryFlushEvent_RemovesQueuedEventsAtOrBelowExecutedUnifiedId()
        {
            // 大きい sequence を適用した場合に、それ以下の未適用イベントが破棄されることを確認する。
            // Ensure applying higher sequence removes unapplied events at or below that unified id.
            var applied = new List<string>();
            _tickState.RecordAppliedTickUnifiedId(60, 0);
            _buffer.EnqueueEvent(60, 1, TrainTickBufferedEvent.Create(() => applied.Add("event1")));
            _buffer.EnqueueEvent(60, 2, TrainTickBufferedEvent.Create(() => applied.Add("event2")));

            Assert.IsTrue(_buffer.TryFlushEvent(TrainTickUnifiedIdUtility.CreateTickUnifiedId(60, 2)));
            Assert.IsFalse(_buffer.TryFlushEvent(TrainTickUnifiedIdUtility.CreateTickUnifiedId(60, 1)));
            CollectionAssert.AreEqual(new[] { "event2" }, applied);
        }
    }
}
