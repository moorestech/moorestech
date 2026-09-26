using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Client.Game.InGame.Context;
using Client.Game.Common;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.TickSynchronization;
using Client.Network.API;
using Client.Tests.Common;
using Client.Tests.TickSynchronization;
using Cysharp.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests
{
    public class TrainFullSnapshotFailurePropagationTest
    {
        [SetUp]
        [TearDown]
        public void ResetShutdown() => GameShutdownEvent.ResetForNewSession();

        [TestCase("rail-decode")]
        [TestCase("train-decode")]
        [TestCase("rail-null")]
        [TestCase("nodes-null")]
        [TestCase("connections-null")]
        [TestCase("train-null")]
        [TestCase("rail-hash")]
        [TestCase("train-hash")]
        [TestCase("watermark")]
        [TestCase("rail-stale")]
        [TestCase("train-stale")]
        [TestCase("rail-missing")]
        public void FailedInitialPair_FaultsStartupAndStopsBufferedReplay(string failure)
        {
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            using var server = services;
            using var client = new TrainSnapshotClientFixture();
            var sink = new CapturedEventSink();
            services.GetRequiredService<EventProtocolProvider>().RegisterPlayer(1, sink);
            var rail = MessagePackSerializer.Deserialize<TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventMessagePack>(sink.Events[0].Payload);
            var train = MessagePackSerializer.Deserialize<TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventMessagePack>(sink.Events[1].Payload);
            switch (failure)
            {
                case "rail-null": rail.Snapshot = null; break;
                case "nodes-null": rail.Snapshot.Nodes = null; break;
                case "connections-null": rail.Snapshot.Connections = null; break;
                case "train-null": train.Snapshots = null; break;
                case "rail-hash": rail.Snapshot.GraphHash ^= 1; break;
                case "train-hash": train.UnitsHash ^= 1; break;
                case "watermark": train.WatermarkTickSequenceId++; break;
                case "rail-stale": client.Context.State.RecordAppliedTickUnifiedId(1); break;
            }
            var railBytes = failure == "rail-decode" ? new byte[] { 0xC1 } : MessagePackSerializer.Serialize(rail);
            var trainBytes = failure == "train-decode" ? new byte[] { 0xC1 } : MessagePackSerializer.Serialize(train);
            var waiting = client.Handler.WaitForInitialApplyAsync().Preserve();
            var save = new SaveProbe();
            GameShutdownEvent.RegisterParticipant(save);
            var fatalCount = 0;
            using var shutdown = GameShutdownEvent.OnGameShutdown.Subscribe(reason =>
            {
                Assert.AreEqual(GameShutdownReason.FatalSynchronizationFailure, reason);
                fatalCount++;
            });
            var previous = ClientContext.VanillaApi;
            using var source = new Subject<EventMessagePack>();
            var exchange = (PacketExchangeManager)FormatterServices.GetUninitializedObject(typeof(PacketExchangeManager));
            TestReflection.SetField(exchange, "_eventPacketSubject", source);
            var dispatcher = new VanillaApiEvent(exchange);
            var api = (VanillaApi)FormatterServices.GetUninitializedObject(typeof(VanillaApi));
            typeof(VanillaApi).GetField("Event").SetValue(api, dispatcher);
            try
            {
                TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", api);
                client.Handler.Initialize();
                using var deltas = new TrainUnitTickDiffBundleEventNetworkHandler(client.Context.Events, client.Trains, client.Context.Hashes);
                deltas.Initialize();
                LogAssert.Expect(LogType.Error, new Regex("TrainFullSnapshot.*initial apply failed"));
                if (failure != "rail-missing")
                    source.OnNext(new EventMessagePack(TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag, railBytes));
                if (failure == "train-stale")
                {
                    dispatcher.InitializeDispatch();
                    client.Context.State.RecordAppliedTickUnifiedId(1);
                }
                source.OnNext(new EventMessagePack(TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag, trainBytes));
                // 失敗後に正常pairとdeltaがreplayされても起動成功へ戻らない。
                // A valid pair and delta replayed after failure must never restore startup success.
                foreach (var message in sink.Events) source.OnNext(message);
                var delta = new TrainUnitTickDiffBundleMessagePack(1, 1, 1, uint.MaxValue, uint.MaxValue, Array.Empty<global::Game.Train.Unit.TrainTickDiffData>());
                source.OnNext(new EventMessagePack(TrainUnitTickDiffBundleEventPacket.EventTag, MessagePackSerializer.Serialize(delta)));
                Assert.DoesNotThrow(() => dispatcher.InitializeDispatch());
                // 初期待機を読む前でも通常終了は保存を開始できない。
                // Normal quit cannot begin saving even before startup observes its failed wait.
                Assert.AreEqual(1, fatalCount);
                Assert.AreEqual(ShutdownFlushResult.AlreadyShutdown, GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit).GetAwaiter().GetResult());
                Assert.AreEqual(0, save.Calls);
                Assert.AreEqual(UniTaskStatus.Faulted, waiting.Status);
                Assert.Throws<TrainInitialSnapshotException>(() => waiting.GetAwaiter().GetResult());
                Assert.IsTrue(client.Context.State.IsStopped);
                Assert.IsFalse(client.Context.Events.TryFlushEvent(1ul << 32 | 1));
                var applied = client.Context.State.GetAppliedTickUnifiedId();
                client.Context.AdvanceController.Advance(0.1f, client.Gate);
                Assert.AreEqual(applied, client.Context.State.GetAppliedTickUnifiedId());
            }
            finally
            {
                TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", previous);
            }
        }

        private sealed class SaveProbe : IGameShutdownParticipant
        {
            public int Calls;
            public UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
            {
                Calls++;
                return UniTask.FromResult(ShutdownFlushResult.Flushed);
            }
        }
    }
}
