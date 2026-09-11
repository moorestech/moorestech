namespace Game.SaveLoad.Snapshot
{
    // 30秒周期・直近2分（ADR 0057）。変えるときはここだけ
    // 30-second period, last two minutes (ADR 0057); change only here
    public static class SnapshotRingConfig
    {
        public const uint PeriodTicks = 600;

        // 剪定直後の最古スナップショットは(世代数-1)周期ぶん前なので、4世代では最悪90秒しか残らない
        // Right after pruning the oldest snapshot is (generations-1) periods old, so four generations leave only 90 seconds
        public const int Generations = 5;
    }
}
