using System.Collections.Generic;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event;
using Server.Event.EventReceive;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    // push型イベントを捕捉するsink登録ヘルパー
    // Registers a capturing sink to observe pushed events
    public class EventTestUtil
    {
        public static CapturedEventSink RegisterCaptureSink(ServiceProvider serviceProvider, int playerId)
        {
            var sink = new CapturedEventSink();
            serviceProvider.GetService<EventProtocolProvider>().RegisterPlayer(playerId, sink);
            // 登録時に自動pushされる初期full snapshotを破棄して空キューから開始する
            // Discard initial full snapshots pushed on registration so tests start empty
            sink.TakeAll();
            return sink;
        }

        // ブロック状態の束イベントから、中身の状態を到着順に取り出す
        // Extracts the block states from bundle events in arrival order
        public static List<BlockStateMessagePack> ExtractBlockStates(List<EventMessagePack> events)
        {
            var states = new List<BlockStateMessagePack>();
            foreach (var eventMessage in events)
            {
                if (eventMessage.Tag != ChangeBlockStateEventPacket.EventTag) continue;
                states.AddRange(MessagePackSerializer.Deserialize<BlockStatesTickBundleMessagePack>(eventMessage.Payload).States);
            }
            return states;
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
