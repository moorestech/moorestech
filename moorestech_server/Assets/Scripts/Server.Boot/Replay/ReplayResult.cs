namespace Server.Boot.Replay
{
    // 再生の結果。到達tickと流し直した件数、到達時点の保存像JSON
    // The replay outcome: the reached tick, how many packets were fed back, and the save image at that point
    public sealed class ReplayResult
    {
        public ulong LoadedTick { get; }
        public ulong ReachedTick { get; }
        public int ReplayedPacketCount { get; }
        public string SnapshotJson { get; }

        public ReplayResult(ulong loadedTick, ulong reachedTick, int replayedPacketCount, string snapshotJson)
        {
            LoadedTick = loadedTick;
            ReachedTick = reachedTick;
            ReplayedPacketCount = replayedPacketCount;
            SnapshotJson = snapshotJson;
        }
    }
}
