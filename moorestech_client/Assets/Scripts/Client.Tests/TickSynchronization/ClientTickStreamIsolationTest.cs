using Client.Game.TickSynchronization;
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
            var firstState = new ClientTickState();
            var secondState = new ClientTickState();
            var first = new TickEventBuffer(firstState);
            var second = new TickEventBuffer(secondState);
            var firstEvent = new CountingTickEvent();
            var secondEvent = new CountingTickEvent();

            // 同じIDを別streamへ積み、片側のwatermarkだけを進める。
            // Enqueue equal ids in separate streams and purge only the first watermark.
            first.EnqueueEvent(1, 1, firstEvent);
            second.EnqueueEvent(1, 1, secondEvent);
            first.DiscardEventsAtOrBelow(TickUnifiedIdUtility.CreateTickUnifiedId(1, 1));
            Assert.IsFalse(first.TryFlushEvent(TickUnifiedIdUtility.CreateTickUnifiedId(1, 1)));
            Assert.IsTrue(second.TryFlushEvent(TickUnifiedIdUtility.CreateTickUnifiedId(1, 1)));
            Assert.AreEqual(0, firstEvent.Count);
            Assert.AreEqual(1, secondEvent.Count);
            Assert.AreEqual(0ul, firstState.GetAppliedTickUnifiedId());
            Assert.AreEqual(TickUnifiedIdUtility.CreateTickUnifiedId(1, 1), secondState.GetAppliedTickUnifiedId());
        }

        [Test]
        public void RepeatedUnappliedId_ReplacesPayloadWithoutAffectingOtherStream()
        {
            var first = new TickEventBuffer(new ClientTickState());
            var second = new TickEventBuffer(new ClientTickState());
            var applied = new List<string>();
            first.EnqueueEvent(1, 1, TickBufferedEvent.Create(() => applied.Add("old")));
            second.EnqueueEvent(1, 1, TickBufferedEvent.Create(() => applied.Add("second")));
            first.EnqueueEvent(1, 1, TickBufferedEvent.Create(() => applied.Add("replacement")));

            Assert.IsTrue(first.TryFlushEvent(TickUnifiedIdUtility.CreateTickUnifiedId(1, 1)));
            Assert.IsTrue(second.TryFlushEvent(TickUnifiedIdUtility.CreateTickUnifiedId(1, 1)));
            first.EnqueueEvent(1, 1, TickBufferedEvent.Create(() => applied.Add("replayed")));
            Assert.IsFalse(first.TryFlushEvent(TickUnifiedIdUtility.CreateTickUnifiedId(1, 1)));
            CollectionAssert.AreEqual(new[] { "replacement", "second" }, applied);
        }

        private sealed class CountingTickEvent : ITickBufferedEvent
        {
            public int Count { get; private set; }
            public void Apply() => Count++;
        }
    }
}
