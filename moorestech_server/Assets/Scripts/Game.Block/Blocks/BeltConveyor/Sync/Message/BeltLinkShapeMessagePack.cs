using System;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using MessagePack;

namespace Game.Block.Blocks.BeltConveyor.Sync.Message
{
    // 全量に載せる接続1本の通信形。相手はsegment番号、機械ならMachine(-1)
    // Wire form of one link in the full state; the partner is a segment number, or Machine (-1) for a machine
    [MessagePackObject]
    public class BeltLinkShapeMessagePack
    {
        [Key(0)] public BeltDirection Direction { get; set; }
        [Key(1)] public BeltEntryDirection EntryDirection { get; set; }
        [Key(2)] public int PartnerSegmentIndex { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BeltLinkShapeMessagePack() { }

        public BeltLinkShapeMessagePack(in BeltLinkShape link)
        {
            Direction = link.Direction;
            EntryDirection = link.EntryDirection;
            PartnerSegmentIndex = link.PartnerSegmentIndex;
        }

        public BeltLinkShape ToShape()
        {
            return new BeltLinkShape(Direction, EntryDirection, PartnerSegmentIndex);
        }
    }
}
