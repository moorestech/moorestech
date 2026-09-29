using Game.PlayerConnection;
using Server.Event;

namespace Server.Protocol.PacketResponse.Util.Handshake
{
    // 実接続と再生接続で登録・解除の順序を共有する
    // Share registration and removal order between live and replayed connections
    public static class PlayerConnectionBinding
    {
        public static bool TryBind(int playerId, PacketResponseContext context, PlayerConnectionRegistry connections, EventProtocolProvider events)
        {
            // 切断処理はバインド直後から走るため、先に接続とイベント宛先を登録する
            // Cleanup may run right after binding, so register the connection and event sink first
            connections.Register(playerId);
            events.RegisterPlayer(playerId, context.EventSink);
            if (context.TryBindPlayerId(playerId)) return true;

            Unregister(playerId, context.EventSink, connections, events);
            return false;
        }

        public static void Unregister(int playerId, IPlayerEventSink eventSink, PlayerConnectionRegistry connections, EventProtocolProvider events)
        {
            events.UnregisterPlayer(playerId, eventSink);
            connections.Unregister(playerId);
        }
    }
}
