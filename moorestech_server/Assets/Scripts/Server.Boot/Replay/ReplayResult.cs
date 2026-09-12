namespace Server.Boot.Replay
{
    // 再生の結果。到達tickと流し直した件数、到達時点の保存像JSON
    // The replay outcome: the reached tick, how many packets were fed back, and the save image at that point
    public sealed class ReplayResult
    {
        public ulong LoadedTick { get; }
        public ulong ReachedTick { get; }
        public int ReplayedPacketCount { get; }

        // 区間(loaded,target]に含まれていた記録件数。0なら渡したログが区間を覆っていない（不一致は非決定性ではない）
        // How many records fell inside (loaded, target]; zero means the given log does not cover the interval, so a mismatch is not non-determinism
        public int InRangePacketCount { get; }

        // 再生から外したセーブ／即時取得要求の件数。再生中に走らせると一時セーブを上書きする
        // How many save / immediate-capture requests were excluded; running them during replay would overwrite the temporary save
        public int ExcludedPacketCount { get; }

        public string SnapshotJson { get; }

        public ReplayResult(ulong loadedTick, ulong reachedTick, int replayedPacketCount, int inRangePacketCount, int excludedPacketCount, string snapshotJson)
        {
            LoadedTick = loadedTick;
            ReachedTick = reachedTick;
            ReplayedPacketCount = replayedPacketCount;
            InRangePacketCount = inRangePacketCount;
            ExcludedPacketCount = excludedPacketCount;
            SnapshotJson = snapshotJson;
        }
    }
}
