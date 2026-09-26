using Core.Update.TickSynchronization;
using NUnit.Framework;

namespace Tests.UnitTest.Core.TickSynchronization
{
    public class TickSequenceStateTest
    {
        [Test]
        public void Streams_ResetIndependentlyAtTheSharedTick()
        {
            var clock = new ServerTickClock();
            var first = new TickSequenceState();
            var second = new TickSequenceState();
            Assert.AreEqual(0u, clock.Tick);
            clock.AdvanceTick();
            first.BeginTick(clock.Tick);
            second.BeginTick(clock.Tick);

            // 同じtickでも各streamの採番とwatermarkは独立する。
            // Sequence allocation and watermark reads stay independent at the same tick.
            Assert.AreEqual(1u, first.NextSequenceId());
            Assert.AreEqual(2u, first.NextSequenceId());
            Assert.AreEqual(1u, second.NextSequenceId());
            var watermark = first.SequenceId;
            Assert.AreEqual(2u, watermark);
            Assert.AreEqual(1u, second.SequenceId);

            // 一方のtick開始だけでは他方の位置を変えない。
            // Beginning a tick in one stream does not move the other stream.
            clock.AdvanceTick();
            first.BeginTick(clock.Tick);
            Assert.AreEqual(0u, first.SequenceId);
            Assert.AreEqual(1u, second.Tick);
            Assert.AreEqual(1u, second.SequenceId);
            second.BeginTick(clock.Tick);
            Assert.AreEqual(0u, second.SequenceId);
            Assert.AreEqual(2u, first.Tick);
            Assert.AreEqual(2u, second.Tick);
        }

        [Test]
        public void UnifiedId_PacksUnsignedTickAndSequence()
        {
            Assert.AreEqual(0x89ABCDEF01234567ul, TrainTickUnifiedIdUtility.CreateTickUnifiedId(0x89ABCDEFu, 0x01234567u));
            Assert.AreEqual(ulong.MaxValue, TrainTickUnifiedIdUtility.CreateTickUnifiedId(uint.MaxValue, uint.MaxValue));
        }
    }
}
