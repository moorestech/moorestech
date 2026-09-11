using Core.Update;

namespace Game.SaveLoad.Snapshot
{
    // 30秒周期・直前2分を必ず残す（ADR 0057）。変えるときはここだけ
    // 30-second period, always keeping the last two minutes (ADR 0057); change only here
    public static class SnapshotRingConfig
    {
        public const uint PeriodTicks = 600;

        // 剪定の基準は時間。件数基準だと即時取得が周期世代の枠を食い、保持幅が縮む
        // Pruning is time-based; a count-based rule would let an immediate capture eat a periodic generation and shrink the window
        public const double RetentionSeconds = 120.0;

        // ディスク保護の上限。保持時間ぶんの周期スナップショットに即時取得のゆとりを足した本数
        // Disk-protection cap: the periodic snapshots covering the retention window plus headroom for immediate captures
        public const int MaxGenerations = 16;

        public static uint RetentionTicks => GameUpdater.SecondsToTicks(RetentionSeconds);
    }
}
