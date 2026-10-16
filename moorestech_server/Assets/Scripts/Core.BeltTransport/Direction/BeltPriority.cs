namespace Core.BeltTransport
{
    // 未接続方向も含む3方向の優先順を、低位から各2bitで1つのintに保持する
    // 先頭（最低位）が最優先。接続の有無は別の方向ビットマスクで持つ
    // Holds the priority order of three directions (including unconnected ones) as 2 bits each from the low end of an int
    // The lowest slot has the highest priority. Connection presence is kept in a separate direction bitmask
    public static class BeltPriority
    {
        // segment生成時にこの値を渡すと、保存値ではなく役割と向きから優先順を初期化する
        // Passing this on segment creation initializes the order from the role and direction instead of a saved value
        public const int InitializeFromDirection = -1;

        public static int Direction(int order, int rank)
        {
            return (order >> (rank * 2)) & 3;
        }

        public static int Create(BeltDirection straightDirection)
        {
            // 直進を先頭に、横2方向を方向番号順に並べる
            // Straight first, then the two side directions in direction-number order
            var straight = (int)straightDirection;
            var side = (straight & 2) ^ 2;
            return straight | (side << 2) | ((side + 1) << 4);
        }

        public static int MoveLast(int order, int direction)
        {
            // 成功した方向を末尾へ移し、残りの相対順序は保つ
            // Move the succeeded direction to the end, keeping the others' relative order
            var first = order & 3;
            if (direction == first) return (order >> 2) | (direction << 4);
            if (direction == ((order >> 2) & 3)) return first | (((order >> 4) & 3) << 2) | (direction << 4);
            return order;
        }

        public static int FirstConnected(int order, int connectionMask)
        {
            // 接続済み方向のうち最優先のもの。接続なしは-1
            // Highest-priority connected direction; -1 when nothing is connected
            for (var rank = 0; rank < 3; rank++)
            {
                var direction = Direction(order, rank);
                if ((connectionMask & (1 << direction)) != 0) return direction;
            }
            return -1;
        }
    }
}
