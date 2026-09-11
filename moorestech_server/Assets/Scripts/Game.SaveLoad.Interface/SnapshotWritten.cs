namespace Game.SaveLoad.Interface
{
    // スナップショット1件の書き出し完了。RequestIdは周期スナップショットなら0
    // One snapshot finished writing; RequestId is 0 for a periodic snapshot
    public sealed class SnapshotWritten
    {
        public long RequestId { get; }
        public ulong Tick { get; }
        public string FilePath { get; }

        public SnapshotWritten(long requestId, ulong tick, string filePath)
        {
            RequestId = requestId;
            Tick = tick;
            FilePath = filePath;
        }
    }
}
