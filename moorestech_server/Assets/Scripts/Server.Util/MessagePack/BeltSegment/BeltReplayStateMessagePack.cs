using System;
using System.Linq;
using Core.Master;
using Game.BeltSegment;
using MessagePack;

namespace Server.Util.MessagePack.BeltSegment
{
    [MessagePackObject]
    public sealed class BeltReplayStateMessagePack
    {
        [Key(0)] public int Capacity { get; set; }
        [Key(1)] public int Speed { get; set; }
        [Key(2)] public BeltSegmentKind Kind { get; set; }
        [Key(3)] public int PriorityIndex { get; set; }
        [Key(4)] public BeltItemStateMessagePack[] Items { get; set; }
        [Key(5)] public BeltItemMessagePack BufferedItem { get; set; }
        [Key(6)] public bool Complete { get; set; }
        [SerializationConstructor]
        [Obsolete("For deserialization only.")] public BeltReplayStateMessagePack() { }
        internal BeltReplayStateMessagePack(BeltReplaySegmentState value)
        { Complete = true;
            Capacity = value.Capacity; Speed = value.Speed; Kind = value.Kind; PriorityIndex = value.PriorityIndex;
            Items = value.Items.Select(x => new BeltItemStateMessagePack(x)).ToArray();
            BufferedItem = value.BufferedItem.HasValue ? new(value.BufferedItem.Value) : null;
        }
        internal BeltReplaySegmentState Decode()
        {
            var items = Items.Select(x => x.Decode()).ToArray();
            return Kind switch
            {
                BeltSegmentKind.Normal => BeltReplaySegmentState.Normal(Capacity, Speed, items),
                BeltSegmentKind.Merge => BeltReplaySegmentState.Merge(Speed, PriorityIndex, items, BufferedItem?.Decode()),
                BeltSegmentKind.Branch => BeltReplaySegmentState.Branch(Capacity, Speed, PriorityIndex, items, BufferedItem?.Decode()),
                _ => throw new ArgumentException("Invalid belt segment kind.")
            };
        }
    }
}
