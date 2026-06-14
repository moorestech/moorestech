namespace Game.Block.Interface.Component
{
    // 壁貫通アイテムハッチの計量用IF。CleanRoomPollutionCalculator が A_hatch = k_hatch · この値 で集計する。
    // Metering interface for the wall-piercing item hatch; CleanRoomPollutionCalculator aggregates A_hatch = k_hatch * this value.
    public interface ICleanRoomItemHatch : IBlockComponent
    {
        // 直近窓の合計搬送個数 / 窓秒。汚染レート項の素。
        // Sum of relayed counts over the recent window / window seconds; basis of the pollution rate term.
        double RecentThroughputPerSecond { get; }
    }
}
