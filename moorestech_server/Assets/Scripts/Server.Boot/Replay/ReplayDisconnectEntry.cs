using Server.Boot.Loop.PacketProcessing;

namespace Server.Boot.Replay
{
    // 切断をパケットと同じtick末尾FIFOへ置き、記録順の位置で適用する
    // Put disconnects in the packet FIFO and apply them at their recorded position
    internal sealed class ReplayDisconnectEntry : ITickEndPacketEntry
    {
        private readonly ReplayConnectionContexts _contexts;
        private readonly int _playerId;

        public bool IsActive => true;

        public ReplayDisconnectEntry(ReplayConnectionContexts contexts, int playerId)
        {
            _contexts = contexts;
            _playerId = playerId;
        }

        public void Process()
        {
            _contexts.Disconnect(_playerId);
        }
    }
}
