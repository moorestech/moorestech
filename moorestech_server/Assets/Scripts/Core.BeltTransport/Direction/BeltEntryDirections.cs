namespace Core.BeltTransport
{
    // 受け入れ側マスから見た搬入元の4方向と高さの差から、12通りの進入方向を作る
    // Builds the 12-way entry direction from the source's 4-way direction seen from the receiving cell and the height difference
    public static class BeltEntryDirections
    {
        private const int HeightStep = 4;

        // 同じ高さのマスから入る
        // Entering from a cell at the same height
        public static BeltEntryDirection Level(BeltDirection inputDirection)
        {
            return (BeltEntryDirection)(int)inputDirection;
        }

        // 1マス上のマスから入る
        // Entering from a cell one step above
        public static BeltEntryDirection FromAbove(BeltDirection inputDirection)
        {
            return (BeltEntryDirection)((int)inputDirection + HeightStep);
        }

        // 1マス下のマスから入る
        // Entering from a cell one step below
        public static BeltEntryDirection FromBelow(BeltDirection inputDirection)
        {
            return (BeltEntryDirection)((int)inputDirection + HeightStep * 2);
        }

        // 高さの差を除いた水平4方向
        // The horizontal 4-way part without the height difference
        public static BeltDirection Horizontal(BeltEntryDirection entryDirection)
        {
            return (BeltDirection)((int)entryDirection % HeightStep);
        }
    }
}
