namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     アイテム搬出入で汚染を持ち込む境界ハッチコンポーネント
    ///     Boundary hatch component that brings in pollution via item transfer
    /// </summary>
    public interface ICleanRoomItemHatch : IBlockComponent
    {
        // 直近のアイテム搬送レート（個/秒）。汚染の kHatch 項に使う
        // Recent item throughput (items/sec) fed into the kHatch pollution term
        double RecentThroughputPerSecond { get; }
    }
}
