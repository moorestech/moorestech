using System.Numerics;

namespace Game.BeltSegment
{
    /// <summary>
    /// アイテムが通過中のマス間と、現在の中心位置。
    /// 呼び出し側は現在マスと搬入元方向を渡し、progressを0～ItemWidthに収める。
    /// progress=0で搬入元の中心、ItemWidthで現在マスの中心。斜面も同じ進行量で補間する。
    /// 所属segmentを変更しても、このインスタンスをアイテムと一緒に引き継ぐ。
    /// </summary>
    public sealed class ItemPosition
    {
        // BeltEntryDirectionの値順。現在マスから搬入元への相対座標。
        // Relative entry-cell offsets in BeltEntryDirection order.
        static readonly Vector3[] entryOffsets =
        {
            new Vector3(0, 1, 0), new Vector3(0, -1, 0), new Vector3(-1, 0, 0), new Vector3(1, 0, 0),
            new Vector3(0, 1, 1), new Vector3(0, -1, 1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1),
            new Vector3(0, 1, -1), new Vector3(0, -1, -1), new Vector3(-1, 0, -1), new Vector3(1, 0, -1)
        };

        public BeltCell CurrentCell { get; private set; }
        public BeltEntryDirection EntryDirection { get; private set; }
        public int Progress { get; private set; }
        public Vector3 Position { get; private set; }

        public ItemPosition(BeltCell currentCell, BeltEntryDirection entryDirection, int progress)
        {
            MoveTo(currentCell, entryDirection, progress);
        }

        public void MoveTo(BeltCell cell, BeltEntryDirection entryDirection, int progress)
        {
            CurrentCell = cell;
            EntryDirection = entryDirection;
            Progress = progress;
            Position = cell.ToPosition() + entryOffsets[(int)entryDirection] * (BeltConstants.ItemWidth - (float)progress);
        }
    }
}
