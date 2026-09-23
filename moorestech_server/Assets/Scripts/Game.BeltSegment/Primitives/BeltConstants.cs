namespace Game.BeltSegment
{
    public static class BeltConstants
    {
        /// <summary>アイテム幅とベルト1マスの長さは同じ。この値を両方に使用する。</summary>
        public const int ItemWidth = 256;
        public const int MaximumCapacity = (int.MaxValue - (ItemWidth - 1)) / ItemWidth;
    }
}
