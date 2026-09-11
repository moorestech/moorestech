namespace Game.SaveLoad.Snapshot
{
    // 30秒周期・直近2分（ADR 0057）。変えるときはここだけ
    // 30-second period, last two minutes (ADR 0057); change only here
    public static class SnapshotRingConfig
    {
        public const uint PeriodTicks = 600;
        public const int Generations = 4;
    }
}
