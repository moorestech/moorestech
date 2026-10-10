using System;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using MessagePack;

namespace Game.Block.Blocks.BeltConveyor.Sync.Message
{
    // 通常segment・buffer→機械の搬出成功1件の通信形
    // Wire form of one successful handoff from a normal segment or buffer into a machine
    [MessagePackObject]
    public class BeltMachineExtractMessagePack
    {
        [Key(0)] public int SegmentIndex { get; set; }
        [Key(1)] public BeltDirection OutputDirection { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BeltMachineExtractMessagePack() { }

        public BeltMachineExtractMessagePack(in BeltMachineExtractRecord record)
        {
            SegmentIndex = record.SegmentIndex;
            OutputDirection = record.OutputDirection;
        }

        public BeltMachineExtractRecord ToRecord()
        {
            return new BeltMachineExtractRecord(SegmentIndex, OutputDirection);
        }
    }
}
