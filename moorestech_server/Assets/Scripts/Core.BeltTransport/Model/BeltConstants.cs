namespace Core.BeltTransport
{
    public static class BeltConstants
    {
        // アイテム幅とベルト1マスの長さは同じ値を使う
        // Item width and the length of one belt cell share this value
        public const int ItemWidth = 256;

        // segment速度の上限（1tickあたりの進行量）
        // Upper bound of segment speed (advance per tick)
        public const int MaxSpeed = ItemWidth / 2;
    }
}
