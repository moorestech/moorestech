// MyBeltConvSegmentのCPU実装をムアステ向けに変更。
// Adapted from MyBeltConvSegment CPU implementation.
namespace Core.BeltTransport
{
    /// <summary>未接続方向も含む3方向を、低位から各2bitで優先順に保持する。</summary>
    internal static class BeltPriority
    {
        internal static int Direction(int order, int rank) => (order >> (rank * 2)) & 3;

        /// <summary>直進を先頭に、横2方向を方向番号順に並べる。</summary>
        internal static int Create(BeltDirection straightDirection)
        {
            int straight = (int)straightDirection;
            int side = (straight & 2) ^ 2;
            return straight | (side << 2) | ((side + 1) << 4);
        }

        internal static int MoveLast(int order, int direction)
        {
            int first = order & 3;
            if (direction == first) return (order >> 2) | (direction << 4);
            if (direction == ((order >> 2) & 3))
                return first | (((order >> 4) & 3) << 2) | (direction << 4);
            return order;
        }

        /// <summary>接続済み方向のうち最優先の方向。接続なしは-1。</summary>
        internal static int FirstConnected(int order, int connectionMask)
        {
            for (int rank = 0; rank < 3; rank++)
            {
                int direction = Direction(order, rank);
                if ((connectionMask & (1 << direction)) != 0) return direction;
            }
            return -1;
        }
    }
}
