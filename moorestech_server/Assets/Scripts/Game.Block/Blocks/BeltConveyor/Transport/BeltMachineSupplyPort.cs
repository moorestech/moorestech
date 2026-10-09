using Core.BeltTransport;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // 機械がベルコンのblockへ押し込むときの受け口。合流なら内部segment、通常・分岐ならそのsegmentが受ける
    // The receiving end for a machine pushing into a belt block: the internal segment for a merge, or the segment itself for a normal or branch
    public readonly struct BeltMachineSupplyPort
    {
        public readonly IBeltReceiver Receiver;
        // 受け側から見た搬入元の方向と、受け側マスへの進入方向
        // Source direction as seen from the receiver, and the entry direction into the receiving cell
        public readonly BeltDirection Direction;
        public readonly BeltEntryDirection EntryDirection;

        public BeltMachineSupplyPort(IBeltReceiver receiver, BeltDirection direction, BeltEntryDirection entryDirection)
        {
            Receiver = receiver;
            Direction = direction;
            EntryDirection = entryDirection;
        }
    }
}
