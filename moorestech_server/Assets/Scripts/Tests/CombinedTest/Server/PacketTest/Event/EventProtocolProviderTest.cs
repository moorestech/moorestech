using System.Collections.Generic;
using NUnit.Framework;
using Server.Event;
using UniRx;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class EventProtocolProviderTest
    {
        // 登録済みsinkへ即時配信を確認
        // Registered sinks receive events immediately
        [Test]
        public void AddEventDispatchesToRegisteredSink()
        {
            var provider = new EventProtocolProvider();
            var sink = new CapturedEventSink();
            provider.RegisterPlayer(1, sink);

            provider.AddEvent(1, "va:event:test", new byte[] { 1 });

            Assert.AreEqual(1, sink.Events.Count);
            Assert.AreEqual("va:event:test", sink.Events[0].Tag);
        }

        // 未登録プレイヤー宛は破棄されることを確認
        // Events for unregistered players are dropped
        [Test]
        public void AddEventForUnregisteredPlayerIsDropped()
        {
            var provider = new EventProtocolProvider();
            var sink = new CapturedEventSink();
            provider.RegisterPlayer(1, sink);

            provider.AddEvent(2, "va:event:test", new byte[] { 1 });

            Assert.AreEqual(0, sink.Events.Count);
        }

        // broadcastが全sinkへ到達
        // Broadcast reaches every registered sink
        [Test]
        public void BroadcastReachesAllRegisteredSinks()
        {
            var provider = new EventProtocolProvider();
            var sink1 = new CapturedEventSink();
            var sink2 = new CapturedEventSink();
            provider.RegisterPlayer(1, sink1);
            provider.RegisterPlayer(2, sink2);

            provider.AddBroadcastEvent("va:event:test", new byte[] { 1 });

            Assert.AreEqual(1, sink1.Events.Count);
            Assert.AreEqual(1, sink2.Events.Count);
        }

        // 登録イベントがsink登録後に同期発火することを確認（初期同期の順序契約）
        // Registration event fires synchronously after the sink becomes usable
        [Test]
        public void RegistrationEventFiresAfterSinkIsUsable()
        {
            var provider = new EventProtocolProvider();
            var sink = new CapturedEventSink();
            provider.OnPlayerEventStreamRegistered.Subscribe(playerId =>
                provider.AddEvent(playerId, "va:event:initial", new byte[] { 1 }));

            provider.RegisterPlayer(1, sink);

            Assert.AreEqual(1, sink.Events.Count);
            Assert.AreEqual("va:event:initial", sink.Events[0].Tag);
        }

        // 切断でsink解除・以後は破棄
        // Unregister removes the sink; later events drop
        [Test]
        public void UnregisterRemovesSink()
        {
            var provider = new EventProtocolProvider();
            var sink = new CapturedEventSink();
            provider.RegisterPlayer(1, sink);

            provider.UnregisterPlayer(1, sink);
            provider.AddEvent(1, "va:event:test", new byte[] { 1 });

            Assert.AreEqual(0, sink.Events.Count);
        }

        // 旧接続の切断は再接続後の新sinkを解除しない（同一性照合）
        // A stale disconnect must not remove a reconnected sink (identity check)
        [Test]
        public void StaleUnregisterDoesNotRemoveReconnectedSink()
        {
            var provider = new EventProtocolProvider();
            var oldSink = new CapturedEventSink();
            var newSink = new CapturedEventSink();
            provider.RegisterPlayer(1, oldSink);
            provider.RegisterPlayer(1, newSink);

            provider.UnregisterPlayer(1, oldSink);
            provider.AddEvent(1, "va:event:test", new byte[] { 1 });

            Assert.AreEqual(1, newSink.Events.Count);
        }
    }
}
