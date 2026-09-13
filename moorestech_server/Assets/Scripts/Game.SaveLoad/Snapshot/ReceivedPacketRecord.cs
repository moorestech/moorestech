namespace Game.SaveLoad.Snapshot
{
    // 受信パケット1件と、それを処理したtick。再生はこのtickを真実として使う
    // One received packet plus the tick that processed it; replay treats this tick as the truth
    public readonly struct ReceivedPacketRecord
    {
        public ulong Tick { get; }
        public byte[] Payload { get; }

        public ReceivedPacketRecord(ulong tick, byte[] payload)
        {
            Tick = tick;
            Payload = payload;
        }
    }
}
