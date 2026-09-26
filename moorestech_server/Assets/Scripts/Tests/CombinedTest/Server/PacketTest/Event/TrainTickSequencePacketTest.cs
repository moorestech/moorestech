using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Core.Update.TickSynchronization;
using Game.Gear.Common;
using Game.Gear.Tick;
using Game.Train.Unit;
using Game.Train.Unit.TickSynchronization;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class TrainTickSequencePacketTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void EmptyWorldBundles_PreservePreviousHashAndCurrentDiffSequence(bool pushSnapshot)
        {
            var originalTick = GameUpdater.CurrentTick;
            try
            {
                var (packets, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
                var firstSink = Handshake(packets, 0);
                var secondSink = Handshake(packets, 1);
                GameUpdater.UpdateOneTick();

                // 個別snapshotを挟んでも両プレイヤーのstreamに採番穴を作らない。
                // A targeted snapshot must not introduce sequence gaps for either player.
                if (pushSnapshot)
                {
                    services.GetService<TrainFullSnapshotEventPacket>().PushFullSnapshots(0, true);
                    var snapshotEvent = firstSink.Events.Single(e => e.Tag == TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag);
                    var snapshot = MessagePackSerializer.Deserialize<TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventMessagePack>(snapshotEvent.Payload);
                    Assert.AreEqual(1u, snapshot.ServerTick);
                    Assert.AreEqual(1u, snapshot.WatermarkTickSequenceId);
                    Assert.IsFalse(secondSink.Events.Any(e => e.Tag == TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag));
                }
                GameUpdater.UpdateOneTick();

                foreach (var sink in new[] { firstSink, secondSink })
                {
                    var bundles = sink.Events.Where(e => e.Tag == TrainUnitTickDiffBundleEventPacket.EventTag)
                        .Select(e => MessagePackSerializer.Deserialize<TrainUnitTickDiffBundleMessagePack>(e.Payload)).ToList();
                    Assert.AreEqual(2, bundles.Count);
                    AssertBundle(bundles[0], 1, 1);
                    AssertBundle(bundles[1], 2, 2);
                    Assert.AreEqual(uint.MaxValue, bundles[1].UnitsHash);
                    Assert.AreEqual(uint.MaxValue, bundles[1].RailGraphHash);
                }
            }
            finally
            {
                GameUpdater.RestoreCurrentTick(originalTick);
            }
        }

        [Test]
        public void RestoredCumulativeTick_NewSessionStartsWireClockAtZero()
        {
            var originalTick = GameUpdater.CurrentTick;
            try
            {
                GameUpdater.RestoreCurrentTick(5000);
                var (packets, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
                var clock = services.GetService<ServerTickClock>();
                var sequence = services.GetService<TrainTickSequenceSource>().Sequence;
                Assert.AreEqual(0u, clock.Tick);
                Assert.AreEqual(0u, sequence.Tick);
                var sink = Handshake(packets, 0);

                // 保存された累積tickと通信sessionのtickは別々に進む。
                // The saved cumulative tick and wire session tick advance separately.
                GameUpdater.UpdateOneTick();
                Assert.AreEqual(5001ul, GameUpdater.CurrentTick);
                Assert.AreEqual(1u, clock.Tick);
                Assert.AreEqual(1u, sequence.Tick);
                var bundleEvent = sink.Events.Single(e => e.Tag == TrainUnitTickDiffBundleEventPacket.EventTag);
                AssertBundle(MessagePackSerializer.Deserialize<TrainUnitTickDiffBundleMessagePack>(bundleEvent.Payload), 1, 1);

                // 乗車入力の受信時刻にもwire tickを使う。
                // Riding input receipt uses the wire tick as well.
                var input = MessagePackSerializer.Serialize(new TrainCarRidingInputProtocol.TrainCarRidingInputMessagePack(0, true, false, false, false));
                packets.GetPacketResponse(input, new PacketResponseContext(sink));
                var recordedInput = services.GetService<TrainCarRidingInputBuffer>().GetLatestMoveInputs().Single();
                Assert.AreEqual(1u, recordedInput.ReceivedTick);
            }
            finally
            {
                GameUpdater.RestoreCurrentTick(originalTick);
            }
        }

        [Test]
        public void GearUpdate_SeesCurrentClockBeforePreviousTrainHashAndCurrentDiff()
        {
            var originalTick = GameUpdater.CurrentTick;
            try
            {
                var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
                var clock = services.GetService<ServerTickClock>();
                var sequence = services.GetService<TrainTickSequenceSource>().Sequence;
                var train = services.GetService<TrainUpdateService>();
                var phases = new List<string>();
                var gearProbe = new GearClockProbe(clock, phases);
                var gears = services.GetService<GearNetworkDatastore>();
                gears.RegisterOverloadTickTarget(gearProbe);
                using var hashSubscription = train.OnHashEvent.Subscribe(hash =>
                {
                    phases.Add($"hash:{hash.Tick}:clock:{clock.Tick}:stream:{sequence.Tick}");
                    Assert.AreEqual(hash.Tick % 4 != 0, hash.UnitsHash == uint.MaxValue);
                    Assert.AreEqual(hash.Tick % 4 != 0, hash.RailGraphHash == uint.MaxValue);
                });
                using var diffSubscription = train.OnPreSimulationDiffEvent.Subscribe(diff =>
                    phases.Add($"diff:{diff.Item1}:clock:{clock.Tick}:stream:{sequence.Tick}"));

                // 実gear更新から時計を観測し、hash計算位置とtrain境界も固定する。
                // Observe the clock inside the real gear update and preserve the hash phase and train boundary.
                for (uint currentTick = 1; currentTick <= 5; currentTick++)
                {
                    phases.Clear();
                    GameUpdater.UpdateOneTick();
                    CollectionAssert.AreEqual(new[]
                    {
                        $"gear:{currentTick}",
                        $"hash:{currentTick - 1}:clock:{currentTick}:stream:{currentTick - 1}",
                        $"diff:{currentTick}:clock:{currentTick}:stream:{currentTick}"
                    }, phases);
                    Assert.AreEqual(currentTick, clock.Tick);
                }
                gears.UnregisterOverloadTickTarget(gearProbe);
            }
            finally
            {
                GameUpdater.RestoreCurrentTick(originalTick);
            }
        }

        private sealed class GearClockProbe : IGearOverloadTickTarget
        {
            private readonly ServerTickClock _clock;
            private readonly List<string> _phases;

            public GearClockProbe(ServerTickClock clock, List<string> phases)
            {
                _clock = clock;
                _phases = phases;
            }

            public void TickOverloadCheck() => _phases.Add($"gear:{_clock.Tick}");
        }

        private static CapturedEventSink Handshake(PacketResponseCreator packets, int playerId)
        {
            var sink = new CapturedEventSink();
            var request = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(playerId, $"Player {playerId}"));
            packets.GetPacketResponse(request, new PacketResponseContext(sink));
            sink.TakeAll();
            return sink;
        }

        private static void AssertBundle(TrainUnitTickDiffBundleMessagePack bundle, uint tick, uint hashSequence)
        {
            Assert.AreEqual(tick, bundle.ServerTick);
            Assert.AreEqual(hashSequence, bundle.HashTickSequenceId);
            Assert.AreEqual(1u, bundle.DiffTickSequenceId);
            Assert.IsEmpty(bundle.Diffs);
        }
    }
}
