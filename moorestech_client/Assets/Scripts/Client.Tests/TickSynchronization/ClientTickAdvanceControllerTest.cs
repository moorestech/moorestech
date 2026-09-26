using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.Network;
using Client.Game.TickSynchronization;
using Core.Update.TickSynchronization;
using NUnit.Framework;
using System.Collections.Generic;

namespace Client.Tests.TickSynchronization
{
    public class ClientTickAdvanceControllerTest
    {
        [Test]
        public void SeparateGates_OnlyStopTheirOwnStream()
        {
            var stopped = new TrainUnitTickState();
            var moving = new TrainUnitTickState();
            var first = new ClientTickAdvanceController(stopped, new TrainUnitFutureMessageBuffer(stopped));
            var second = new ClientTickAdvanceController(moving, new TrainUnitFutureMessageBuffer(moving));
            var closed = new FixedGate(false);
            var open = new FixedGate(true);
            for (var frame = 0; frame < 100; frame++)
            {
                first.Advance(0.05f, closed);
                second.Advance(0.05f, open);
            }
            Assert.AreEqual(0u, stopped.GetTick());
            Assert.Greater(moving.GetTick(), 0u);
        }

        [Test]
        public void ReverseArrival_FlushesExactKeysBeforeGateAndDoesNotReapply()
        {
            var state = new TrainUnitTickState();
            var events = new TrainUnitFutureMessageBuffer(state);
            var controller = new ClientTickAdvanceController(state, events);
            var applied = new List<int>();
            var gate = new FixedGate(false);
            events.EnqueueEvent(0, 2, TrainTickBufferedEvent.Create(() => applied.Add(2)));
            events.EnqueueEvent(0, 1, TrainTickBufferedEvent.Create(() => applied.Add(1)));

            // 連続したeventだけを適用してから次のIDをgateへ渡す。
            // Apply contiguous events before asking the gate about the next id.
            var renderTick = controller.Advance(0.1f, gate);
            CollectionAssert.AreEqual(new[] { 1, 2 }, applied);
            Assert.AreEqual(TrainTickUnifiedIdUtility.CreateTickUnifiedId(0, 3), gate.LastId);
            Assert.AreEqual(1d, renderTick);
            events.EnqueueEvent(0, 1, TrainTickBufferedEvent.Create(() => applied.Add(99)));
            controller.Advance(0.1f, gate);
            CollectionAssert.AreEqual(new[] { 1, 2 }, applied);
        }

        [Test]
        public void SequenceGap_StopsEventFlushAtMissingExactKey()
        {
            var state = new TrainUnitTickState();
            var events = new TrainUnitFutureMessageBuffer(state);
            var controller = new ClientTickAdvanceController(state, events);
            var gate = new FixedGate(false);
            var count = 0;
            events.EnqueueEvent(0, 2, TrainTickBufferedEvent.Create(() => count++));
            controller.Advance(0.1f, gate);
            Assert.AreEqual(0, count);
            Assert.AreEqual(1ul, gate.LastId);
            Assert.AreEqual(0ul, state.GetAppliedTickUnifiedId());
        }

        [Test]
        public void LargeFrame_AdvancesAtMostFourTicks()
        {
            var state = new TrainUnitTickState();
            var controller = new ClientTickAdvanceController(state, new TrainUnitFutureMessageBuffer(state));
            var renderTick = controller.Advance(10f, new FixedGate(true));
            Assert.AreEqual(4u, state.GetTick());
            Assert.AreEqual(4d, renderTick);
        }

        private sealed class FixedGate : ITrainUnitHashTickGate
        {
            private readonly bool _canAdvance;
            public ulong LastId { get; private set; }
            public FixedGate(bool canAdvance) => _canAdvance = canAdvance;
            public bool CanAdvanceTick(ulong currentTickUnifiedId)
            {
                LastId = currentTickUnifiedId;
                return _canAdvance;
            }
        }
    }
}
