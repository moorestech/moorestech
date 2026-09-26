using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.Network;
using Core.Update.TickSynchronization;
using NUnit.Framework;
using System.Collections.Generic;

namespace Client.Tests.TickSynchronization
{
    public class ClientTickStreamIsolationTest
    {
        [Test]
        public void EqualIdsInSeparateStreams_DoNotCollideOrPurgeEachOther()
        {
            var firstState = new TrainUnitTickState();
            var secondState = new TrainUnitTickState();
            var first = new TrainUnitFutureMessageBuffer(firstState);
            var second = new TrainUnitFutureMessageBuffer(secondState);
            var firstEvent = new CountingTickEvent();
            var secondEvent = new CountingTickEvent();

            // 同じIDを別streamへ積み、片側のwatermarkだけを進める。
            // Enqueue equal ids in separate streams and purge only the first watermark.
            first.EnqueueEvent(1, 1, firstEvent);
            second.EnqueueEvent(1, 1, secondEvent);
            first.DiscardEventsAtOrBelow(TrainTickUnifiedIdUtility.CreateTickUnifiedId(1, 1));
            Assert.IsFalse(first.TryFlushEvent(TrainTickUnifiedIdUtility.CreateTickUnifiedId(1, 1)));
            Assert.IsTrue(second.TryFlushEvent(TrainTickUnifiedIdUtility.CreateTickUnifiedId(1, 1)));
            Assert.AreEqual(0, firstEvent.Count);
            Assert.AreEqual(1, secondEvent.Count);
            Assert.AreEqual(0ul, firstState.GetAppliedTickUnifiedId());
            Assert.AreEqual(TrainTickUnifiedIdUtility.CreateTickUnifiedId(1, 1), secondState.GetAppliedTickUnifiedId());
        }

        [Test]
        public void RepeatedUnappliedId_ReplacesPayloadWithoutAffectingOtherStream()
        {
            var first = new TrainUnitFutureMessageBuffer(new TrainUnitTickState());
            var second = new TrainUnitFutureMessageBuffer(new TrainUnitTickState());
            var applied = new List<string>();
            first.EnqueueEvent(1, 1, TrainTickBufferedEvent.Create(() => applied.Add("old")));
            second.EnqueueEvent(1, 1, TrainTickBufferedEvent.Create(() => applied.Add("second")));
            first.EnqueueEvent(1, 1, TrainTickBufferedEvent.Create(() => applied.Add("replacement")));

            Assert.IsTrue(first.TryFlushEvent(TrainTickUnifiedIdUtility.CreateTickUnifiedId(1, 1)));
            Assert.IsTrue(second.TryFlushEvent(TrainTickUnifiedIdUtility.CreateTickUnifiedId(1, 1)));
            first.EnqueueEvent(1, 1, TrainTickBufferedEvent.Create(() => applied.Add("replayed")));
            Assert.IsFalse(first.TryFlushEvent(TrainTickUnifiedIdUtility.CreateTickUnifiedId(1, 1)));
            CollectionAssert.AreEqual(new[] { "replacement", "second" }, applied);
        }

        private sealed class CountingTickEvent : ITrainTickBufferedEvent
        {
            public int Count { get; private set; }
            public void Apply() => Count++;
        }
    }
}
