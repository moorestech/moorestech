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

        // 成立した部屋に属しているかを判定段から受け取る。属している間だけフィルターを消費する
        // Receive from the clean-room pass whether it serves a detected room; filters are consumed only while serving
        void SetServingRoom(bool isServingRoom);

        // 判定段から最後に受け取った在室。セーブされ、ロード時に判定段の前tick集合を作り直すのに使う
        // The serving state last received from the pass; it is saved and rebuilds the pass's previous-tick set on load
        bool IsServingRoom { get; }
    }
}
