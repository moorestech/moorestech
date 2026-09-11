using Core.Update;
using Game.SaveLoad.Snapshot;
using Server.Boot.Loop.PacketProcessing;
using Server.Protocol;

namespace Server.Boot.Replay
{
    // 記録済みパケットをtick末尾で処理する項目。応答は捨てる（再生に受信者はいない）
    // Processes a recorded packet at tick end; responses are discarded since replay has no receiver
    public sealed class ReplayPacketEntry : ITickEndPacketEntry
    {
        private readonly PacketResponseCreator _packetResponseCreator;
        private readonly PacketResponseContext _context;
        private readonly byte[] _payload;

        // 記録するログ。再生時は記録しないのでnullを渡す（記録先が無いことは縮退ではなく設計）
        // The log to append to; replay passes null because it must not record, which is by design rather than a degradation
        private readonly ReceivedPacketLog _packetLog;

        public bool IsActive => true;

        public ReplayPacketEntry(PacketResponseCreator packetResponseCreator, PacketResponseContext context, byte[] payload, ReceivedPacketLog packetLog)
        {
            _packetResponseCreator = packetResponseCreator;
            _context = context;
            _payload = payload;
            _packetLog = packetLog;
        }

        public void Process()
        {
            _packetLog?.Append(GameUpdater.CurrentTick, _payload);
            _packetResponseCreator.GetPacketResponse(_payload, _context);
        }
    }
}
