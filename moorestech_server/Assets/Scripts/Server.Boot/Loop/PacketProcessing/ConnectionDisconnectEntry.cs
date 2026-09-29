using Core.Update;
using Game.PlayerConnection;
using Game.SaveLoad.Snapshot;
using Server.Event;
using Server.Protocol;
using UnityEngine;

namespace Server.Boot.Loop.PacketProcessing
{
    // 記録とイベント宛先の解除をパケットと同じFIFOで確定し、ログの順序を受信順に合わせる
    // Commit the record and the event-sink removal in the packet FIFO so the log order follows arrival order
    // 接続集合からの解除は受信スレッドで即時に済ませる。tick末尾まで遅らせると切断済みの身元が接続中に見える
    // Removal from the connection set happens synchronously on the receive thread; deferring it would show a closed identity as connected
    public sealed class ConnectionDisconnectEntry : ITickEndPacketEntry
    {
        private readonly IPlayerEventSink _eventSink;
        private readonly int? _playerId;
        private readonly EventProtocolProvider _events;
        private readonly ReceivedPacketLog _log;

        private ConnectionDisconnectEntry(IPlayerEventSink eventSink, int? playerId,
            EventProtocolProvider events, ReceivedPacketLog log)
        {
            _eventSink = eventSink;
            _playerId = playerId;
            _events = events;
            _log = log;
        }

        // 接続の後始末の唯一の入口。同期で行う解除とFIFOへ積む記録の順序をここだけが決める
        // The single entry for tearing a connection down; only this decides what unregisters now and what goes onto the FIFO
        public static void Schedule(PacketResponseContext context, ReceiveQueueProcessor receiver,
            TickEndPacketQueue queue, PlayerConnectionRegistry connections, EventProtocolProvider events,
            ReceivedPacketLog log)
        {
            // 新規受信だけ止め、未紐づけでも切断を積む。Freeze後なら次tickへ回る
            // Stop new receives and enqueue even unbound disconnects; after Freeze they run next tick
            receiver.Dispose();

            // 接続集合からの解除は即時。tick末尾へ遅らせるとIsConnectedが切断済みの身元を接続中と答える
            // Removal from the connection set happens now; deferring it would make IsConnected call a closed identity connected
            var playerId = context.MarkClosedAndGetPlayerId();
            if (playerId.HasValue) connections.Unregister(playerId.Value);
            queue.Enqueue(new ConnectionDisconnectEntry(context.EventSink, playerId, events, log));
        }

        public void Process()
        {
            if (_playerId.HasValue)
            {
                _events.UnregisterPlayer(_playerId.Value, _eventSink);
                _log.AppendDisconnect(GameUpdater.CurrentTick, _playerId.Value);
                return;
            }

            // 未紐づけに解除対象はないが、位置を再生できるようnullで記録する
            // An unbound connection has nothing to remove, but record its FIFO position as null
            Debug.Log("未紐づけ接続の切断はプレイヤー登録解除を行いません");
            _log.AppendDisconnect(GameUpdater.CurrentTick, null);
        }
    }
}
