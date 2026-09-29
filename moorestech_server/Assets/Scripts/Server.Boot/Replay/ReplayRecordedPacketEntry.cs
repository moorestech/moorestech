using Server.Boot.Loop.PacketProcessing;
using Server.Protocol;

namespace Server.Boot.Replay
{
    // 実行時に接続を引くため、同tickの切断後に続くパケットは新しい接続を使う
    // Resolve the context at execution so packets after a same-tick disconnect use a new connection
    internal sealed class ReplayRecordedPacketEntry : ITickEndPacketEntry
    {
        private readonly PacketResponseCreator _creator;
        private readonly ReplayConnectionContexts _contexts;
        private readonly int? _playerId;
        private readonly byte[] _payload;

        public ReplayRecordedPacketEntry(PacketResponseCreator creator, ReplayConnectionContexts contexts,
            int? playerId, byte[] payload)
        {
            _creator = creator;
            _contexts = contexts;
            _playerId = playerId;
            _payload = payload;
        }

        public void Process()
        {
            _creator.GetPacketResponse(_payload, _contexts.ContextFor(_playerId));
        }
    }
}
