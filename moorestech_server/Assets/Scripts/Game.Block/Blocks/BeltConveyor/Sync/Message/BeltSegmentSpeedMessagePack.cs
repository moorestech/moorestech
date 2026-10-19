using System;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using MessagePack;

namespace Game.Block.Blocks.BeltConveyor.Sync.Message
{
    [MessagePackObject]
    public class BeltSegmentSpeedMessagePack
    {
        [Key(0)] public int SegmentIndex { get; set; }
        [Key(1)] public int Speed { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BeltSegmentSpeedMessagePack() { }

        public BeltSegmentSpeedMessagePack(in BeltSegmentSpeedRecord record)
        {
            SegmentIndex = record.SegmentIndex;
            Speed = record.Speed;
        }

        public BeltSegmentSpeedRecord ToRecord()
        {
            return new BeltSegmentSpeedRecord(SegmentIndex, Speed);
        }
    }
}
