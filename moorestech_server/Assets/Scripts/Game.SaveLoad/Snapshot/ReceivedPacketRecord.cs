namespace Game.SaveLoad.Snapshot
{
    public enum ReceivedPacketRecordKind : byte
    {
        Packet = 1,
        Disconnect = 2,
    }

    // 受信パケット1件と、それを処理したtick。再生はこのtickを真実として使う
    // One received packet plus the tick that processed it; replay treats this tick as the truth
    public readonly struct ReceivedPacketRecord
    {
        public ulong Tick { get; }
        public int? PlayerId { get; }
        public byte[] Payload { get; }
        public ReceivedPacketRecordKind Kind { get; }

        public ReceivedPacketRecord(ulong tick, int? playerId, byte[] payload, ReceivedPacketRecordKind kind)
        {
            Tick = tick;
            PlayerId = playerId;
            Payload = payload;
            Kind = kind;
        }
    }
}
