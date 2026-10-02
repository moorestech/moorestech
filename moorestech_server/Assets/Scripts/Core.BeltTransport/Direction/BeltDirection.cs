namespace Core.BeltTransport
{
    // 接続用の4方向。XZが水平でY上向き。前は+Z、後は-Z、左は-X、右は+X
    // Four connection directions. XZ is horizontal, Y is up. Front=+Z, Back=-Z, Left=-X, Right=+X
    public enum BeltDirection : int
    {
        // 搬入予約なし。接続には使わない
        // No input reservation. Never used for connections
        None = -1,
        Front = 0,
        Back = 1,
        Left = 2,
        Right = 3
    }
}
