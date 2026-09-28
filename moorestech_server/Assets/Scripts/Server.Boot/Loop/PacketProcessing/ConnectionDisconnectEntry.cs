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
        private readonly PacketResponseContext _context;
        private readonly PlayerConnectionRegistry _connections;
        private readonly EventProtocolProvider _events;
        private readonly ReceivedPacketLog _log;

        public bool IsActive => true;

        private ConnectionDisconnectEntry(PacketResponseContext context,
            PlayerConnectionRegistry connections, EventProtocolProvider events, ReceivedPacketLog log)
        {
            _context = context;
            _connections = connections;
            _events = events;
            _log = log;
        }

        public static void Schedule(PacketResponseContext context, ReceiveQueueProcessor receiver,
            TickEndPacketQueue queue, PlayerConnectionRegistry connections, EventProtocolProvider events,
            ReceivedPacketLog log)
        {
            // 新規受信だけ止め、未紐づけでも切断を積む。Freeze後なら次tickへ回る
            // Stop new receives and enqueue even unbound disconnects; after Freeze they run next tick
            receiver.Dispose();
            queue.Enqueue(new ConnectionDisconnectEntry(context, connections, events, log));
        }

        public void Process()
        {
            // closeもFIFOで確定する。先行ハンドシェイクが割り当てたIDをここで読む
            // Close in the FIFO too, reading any ID assigned by an earlier handshake
            var playerId = _context.MarkClosedAndGetPlayerId();
            if (playerId.HasValue)
            {
                PlayerConnectionBinding.Unregister(playerId.Value, _context.EventSink, _connections, _events);
                _log.AppendDisconnect(GameUpdater.CurrentTick, playerId.Value);
                return;
            }

            // 未紐づけに解除対象はないが、位置を再生できるようnullで記録する
            // An unbound connection has nothing to remove, but record its FIFO position as null
            Debug.Log("未紐づけ接続の切断はプレイヤー登録解除を行いません");
            _log.AppendDisconnect(GameUpdater.CurrentTick, null);
        }
    }
}
