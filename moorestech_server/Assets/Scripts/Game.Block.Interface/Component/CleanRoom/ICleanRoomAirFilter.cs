namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     部屋内部に設置され換気回数で部屋のクラスを上げる清浄機コンポーネント
    ///     Air purifier component placed inside a room that raises the room class through its air change rate
    /// </summary>
    public interface ICleanRoomAirFilter : IBlockComponent
    {
        // 電力割合・フィルター有無を織り込んだ実効除去体積 q（毎秒）
        // Effective removal volume q per second reflecting power ratio and filter presence
        double RemovalVolumePerSecond { get; }

        // UI表示用に電力割合・フィルター数・残り寿命を読む
        // Read the power ratio, filter count and remaining life for UI display
        CleanRoomAirFilterStatus GetStatus();
    }
}
