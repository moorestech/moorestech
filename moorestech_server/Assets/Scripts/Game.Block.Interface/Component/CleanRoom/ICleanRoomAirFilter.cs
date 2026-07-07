namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     部屋内部に設置され空気中の不純物を除去する清浄機コンポーネント
    ///     Air purifier component placed inside a room to remove airborne impurities
    /// </summary>
    public interface ICleanRoomAirFilter : IBlockComponent
    {
        // 電力割合・フィルター有無を織り込んだ実効除去体積 q（毎秒）
        // Effective removal volume q per second reflecting power ratio and filter presence
        double RemovalVolumePerSecond { get; }

        // 今tickに除去した不純物量をフィルター摩耗として押し込む
        // Push the impurity removed this tick into the filter as wear
        void ApplyRemovedImpurity(double removed);
    }
}
