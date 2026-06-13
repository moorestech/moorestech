namespace Game.Block.Interface.Component
{
    // エアフィルター1台の除去能力 q（m³/秒）をデータストアが読むための口。
    // フェーズ3の CleanRoomAirFilterComponent が実装（実効値=q×電力割合×フィルター残有無）。
    // Datastore-facing view of one air filter's removal volume q (m^3/sec); implemented in phase 3.
    public interface ICleanRoomAirFilter : IBlockComponent
    {
        double RemovalVolumePerSecond { get; }
    }
}
