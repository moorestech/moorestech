using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Server.Event;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    // push型イベントをテストで捕捉するためのsink登録ヘルパー
    // Helper that registers a capturing sink to observe pushed events in tests
    public class EventTestUtil
    {
        public static CapturedEventSink RegisterCaptureSink(ServiceProvider serviceProvider, int playerId)
        {
            var sink = new CapturedEventSink();
            serviceProvider.GetService<EventProtocolProvider>().RegisterPlayer(playerId, sink);
            return sink;
        }
    }

    // 送信されたイベントをListに溜めるテスト用sink
    // Test sink that captures dispatched events into a list
    public class CapturedEventSink : IPlayerEventSink
    {
        public List<EventMessagePack> Events { get; } = new();

        public void EnqueueEvent(EventMessagePack eventMessagePack)
        {
            Events.Add(eventMessagePack);
        }

        // 現在までのイベントを取り出してクリアする（旧ポーリングの「全返し＆Clear」相当）
        // Take all captured events and clear, mirroring the old poll-and-clear semantics
        public List<EventMessagePack> TakeAll()
        {
            var taken = new List<EventMessagePack>(Events);
            Events.Clear();
            return taken;
        }
    }
}
