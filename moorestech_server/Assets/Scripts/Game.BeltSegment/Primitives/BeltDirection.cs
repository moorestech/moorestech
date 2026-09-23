namespace Game.BeltSegment
{
    /// <summary>接続用の4方向。前は+Y、後は-Y、左は-X、右は+X。</summary>
    public enum BeltDirection : int
    {
        /// <summary>搬入予約なし。接続には使わない。</summary>
        None = -1,
        Front = 0,
        Back = 1,
        Left = 2,
        Right = 3
    }

    internal static class BeltDirections
    {
        // 0と1、2と3をそれぞれ反対方向として組にする。
        internal static BeltDirection Opposite(BeltDirection direction)
            => (BeltDirection)((int)direction ^ 1);
    }
}
