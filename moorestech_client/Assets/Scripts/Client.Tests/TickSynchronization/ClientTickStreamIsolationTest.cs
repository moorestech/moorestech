using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.Network;
using Core.Update.TickSynchronization;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Train.Network.TickSynchronization;
using Client.Starter;
using VContainer;

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

        [Test]
        public void RuntimeRegistration_ResolvesContextOwnedStateAndBuffers()
        {
            using var resolver = BuildRuntimeContainer();
            var context = resolver.Resolve<TrainTickContext>();

            // 実登録を通し、直接注入とcontextの状態が二重化しないことを確かめる。
            // Use production registration to ensure direct injection and context share one state.
            Assert.AreSame(context, resolver.Resolve<TrainTickContext>());
            Assert.AreSame(context.State, resolver.Resolve<TrainUnitTickState>());
            Assert.AreSame(context.Events, resolver.Resolve<TrainUnitFutureMessageBuffer>());
            Assert.AreSame(context.Hashes, resolver.Resolve<TrainUnitHashBuffer>());

            var bufferedEvent = new CountingTickEvent();
            resolver.Resolve<TrainUnitFutureMessageBuffer>().EnqueueEvent(1, 1, bufferedEvent);
            Assert.IsTrue(context.Events.TryFlushEvent(TrainTickUnifiedIdUtility.CreateTickUnifiedId(1, 1)));
            Assert.AreEqual(1, bufferedEvent.Count);
            Assert.AreEqual(TrainTickUnifiedIdUtility.CreateTickUnifiedId(1, 1), context.State.GetAppliedTickUnifiedId());
        }

        [Test]
        public void SeparateRuntimeRegistrations_DoNotShareTickStateOrBuffers()
        {
            using var firstResolver = BuildRuntimeContainer();
            using var secondResolver = BuildRuntimeContainer();
            var first = firstResolver.Resolve<TrainTickContext>();
            var second = secondResolver.Resolve<TrainTickContext>();
            Assert.AreNotSame(first.State, second.State);
            Assert.AreNotSame(first.Events, second.Events);
            Assert.AreNotSame(first.Hashes, second.Hashes);

            // 片側の既適用位置は別登録のevent/hash受信に波及しない。
            // Advancing one registration does not reject events or hashes in another.
            firstResolver.Resolve<TrainUnitTickState>().RecordAppliedTickUnifiedId(2, 0);
            var bufferedEvent = new CountingTickEvent();
            secondResolver.Resolve<TrainUnitFutureMessageBuffer>().EnqueueEvent(1, 1, bufferedEvent);
            secondResolver.Resolve<TrainUnitHashBuffer>().EnqueueHash(11, 12, 1, 2);
            Assert.IsTrue(second.Events.TryFlushEvent(TrainTickUnifiedIdUtility.CreateTickUnifiedId(1, 1)));
            Assert.IsTrue(second.Hashes.TryDequeueHashAtTickSequenceId(TrainTickUnifiedIdUtility.CreateTickUnifiedId(1, 2), out _));
            Assert.AreEqual(1, bufferedEvent.Count);
            Assert.AreEqual(TrainTickUnifiedIdUtility.CreateTickUnifiedId(2, 0), first.State.GetAppliedTickUnifiedId());
        }

        private static IObjectResolver BuildRuntimeContainer()
        {
            var builder = new ContainerBuilder();
            var registration = typeof(MainGameStarter).Assembly.GetType("Client.Starter.Registration.MainGameInteractionRegistration");
            registration.GetMethod("RegisterRuntimeServices", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { builder });
            return builder.Build();
        }

        private sealed class CountingTickEvent : ITrainTickBufferedEvent
        {
            public int Count { get; private set; }
            public void Apply() => Count++;
        }
    }
}
