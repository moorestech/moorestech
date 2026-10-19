using System;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using MessagePack;

namespace Game.Block.Blocks.BeltConveyor.Sync.Message
{
    // 機械→ベルコンの搬入成功1件の通信形
    // Wire form of one successful machine-to-belt push
    [MessagePackObject]
    public class BeltMachineInsertMessagePack
    {
        [Key(0)] public int SegmentIndex { get; set; }
        [Key(1)] public BeltDirection InputDirection { get; set; }
        [Key(2)] public ItemId ItemId { get; set; }
        [Key(3)] public long ItemInstanceId { get; set; }
        [Key(4)] public BeltEntryDirection EntryDirection { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BeltMachineInsertMessagePack() { }

        public BeltMachineInsertMessagePack(in BeltMachineInsertRecord record)
        {
            SegmentIndex = record.SegmentIndex;
            InputDirection = record.InputDirection;
            ItemId = record.ItemId;
            ItemInstanceId = record.ItemInstanceId.AsPrimitive();
            EntryDirection = record.EntryDirection;
        }

        public BeltMachineInsertRecord ToRecord()
        {
            return new BeltMachineInsertRecord(SegmentIndex, InputDirection, new BeltItem(ItemId, new ItemInstanceId(ItemInstanceId), EntryDirection));
        }
    }
}
