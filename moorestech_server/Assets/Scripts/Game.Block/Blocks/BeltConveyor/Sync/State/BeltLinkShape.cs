using Core.BeltTransport;

namespace Game.Block.Blocks.BeltConveyor.Sync.State
{
    // 全量に載せる接続1本。相手はsegment番号で指し、機械ならMachine(-1)
    // One link in the full state; the partner is a segment number, or Machine (-1) for a machine
    public readonly struct BeltLinkShape
    {
        public const int Machine = -1;

        // 出力なら末尾マスから相手へ、入力なら先頭マスから送り元へ向かう水平方向
        // Horizontal direction from the last cell toward the partner for outputs, or from the head cell toward the source for inputs
        public readonly BeltDirection Direction;
        // 受け取る側のマスから見た12通りの進入方向
        // 12-way entry direction as seen from the receiving cell
        public readonly BeltEntryDirection EntryDirection;
        public readonly int PartnerSegmentIndex;

        public bool IsMachine => PartnerSegmentIndex == Machine;

        public BeltLinkShape(BeltDirection direction, BeltEntryDirection entryDirection, int partnerSegmentIndex)
        {
            Direction = direction;
            EntryDirection = entryDirection;
            PartnerSegmentIndex = partnerSegmentIndex;
        }
    }
}
