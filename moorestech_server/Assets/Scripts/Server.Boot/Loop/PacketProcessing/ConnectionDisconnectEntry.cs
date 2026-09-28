using Core.Update;
using Game.PlayerConnection;
using Game.SaveLoad.Snapshot;
using Server.Event;
using Server.Protocol;
using Server.Protocol.PacketResponse.Util.Handshake;
using UnityEngine;

namespace Server.Boot.Loop.PacketProcessing
{
    // 切断をパケットと同じFIFOで確定し、接続集合とログの順序を一致させる
    // Commit disconnects in the packet FIFO so connection state and log order agree
    public sealed class ConnectionDisconnectEntry : ITickEndPacketEntry
    {
        private readonly int _playerId;
        private readonly IPlayerEventSink _eventSink;
        private readonly PlayerConnectionRegistry _connections;
        private readonly EventProtocolProvider _events;
        private readonly ReceivedPacketLog _log;

        public bool IsActive => true;

        private ConnectionDisconnectEntry(int playerId, IPlayerEventSink eventSink,
            PlayerConnectionRegistry connections, EventProtocolProvider events, ReceivedPacketLog log)
        {
            _playerId = playerId;
            _eventSink = eventSink;
            _connections = connections;
            _events = events;
            _log = log;
        }

        public static void Schedule(PacketResponseContext context, ReceiveQueueProcessor receiver,
            TickEndPacketQueue queue, PlayerConnectionRegistry connections, EventProtocolProvider events,
            ReceivedPacketLog log)
        {
            // 受信を先に閉じ、既存パケットの後ろへ切断を積む。Freeze後の投入は次tickへ回る
            // Close receive first, then enqueue after prior packets; an enqueue after Freeze runs next tick
            var playerId = context.MarkClosedAndGetPlayerId();
            receiver.Dispose();
            if (!playerId.HasValue)
            {
                Debug.Log("未紐づけ接続の切断はプレイヤー登録解除を行いません");
                return;
            }
            queue.Enqueue(new ConnectionDisconnectEntry(playerId.Value, context.EventSink, connections, events, log));
        }

        public void Process()
        {
            // 最大1tickは旧接続が接続中に見える。解除と記録は同じtick末尾位置で行う
            // The old connection can remain visible for one tick; removal and logging share one FIFO position
            PlayerConnectionBinding.Unregister(_playerId, _eventSink, _connections, _events);
            _log.AppendDisconnect(GameUpdater.CurrentTick, _playerId);
        }
    }
}
