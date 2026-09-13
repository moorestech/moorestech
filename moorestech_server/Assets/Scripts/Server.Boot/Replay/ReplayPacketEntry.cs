using Server.Boot.Loop.PacketProcessing;
using Server.Protocol;

namespace Server.Boot.Replay
{
    // 記録済みパケットをtick末尾で処理する項目。応答は捨て、常時記録へは一切書かない（再生由来の偽パケットを混ぜないため）
    // Processes a recorded packet at tick end; responses are discarded and nothing is written back to capture, so replay never injects fake packets
    public sealed class ReplayPacketEntry : ITickEndPacketEntry
    {
        private readonly PacketResponseCreator _packetResponseCreator;
        private readonly PacketResponseContext _context;
        private readonly byte[] _payload;

        public bool IsActive => true;

        public ReplayPacketEntry(PacketResponseCreator packetResponseCreator, PacketResponseContext context, byte[] payload)
        {
            _packetResponseCreator = packetResponseCreator;
            _context = context;
            _payload = payload;
        }

        public void Process()
        {
            _packetResponseCreator.GetPacketResponse(_payload, _context);
        }
    }
}
