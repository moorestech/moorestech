using System;
using System.Linq;
using Core.Master;
using Game.BeltSegment;
using MessagePack;

namespace Server.Util.MessagePack.BeltSegment
{
    [MessagePackObject]
    public sealed class BeltReplayTickMessagePack
    {
        [Key(0)] public BeltSpeedMessagePack[] SpeedChanges { get; set; }
        [Key(1)] public int[] ReadyInputs { get; set; }
        [Key(2)] public int[] SuccessfulOutputs { get; set; }
        [Key(3)] public BeltInsertionMessagePack[] Insertions { get; set; }
        [Key(4)] public bool Complete { get; set; }
        [SerializationConstructor]
        [Obsolete("For deserialization only.")] public BeltReplayTickMessagePack() { }
        internal BeltReplayTickMessagePack(BeltReplayTick value)
        { Complete = true;
            SpeedChanges = value.SpeedChanges.Select(x => new BeltSpeedMessagePack(x)).ToArray();
            ReadyInputs = (int[])value.ReadyInputs.Clone(); SuccessfulOutputs = (int[])value.SuccessfulOutputs.Clone();
            Insertions = value.Insertions.Select(x => new BeltInsertionMessagePack(x)).ToArray();
        }
        internal BeltReplayTick Decode() => new(SpeedChanges.Select(x => x.Decode()).ToArray(),
            (int[])ReadyInputs.Clone(), (int[])SuccessfulOutputs.Clone(), Insertions.Select(x => x.Decode()).ToArray());
    }
    [MessagePackObject]
    public sealed class BeltSpeedMessagePack
    {
        [Key(0)] public int SegmentId { get; set; }
        [Key(1)] public int Speed { get; set; }
        [Key(2)] public bool Complete { get; set; }
        [SerializationConstructor]
        [Obsolete("For deserialization only.")] public BeltSpeedMessagePack() { }
        internal BeltSpeedMessagePack(BeltReplaySpeedChange value) { Complete = true; SegmentId = value.SegmentId; Speed = value.Speed; }
        internal BeltReplaySpeedChange Decode() => new(SegmentId, Speed);
    }
    [MessagePackObject]
    public sealed class BeltInsertionMessagePack
    {
        [Key(0)] public int InputId { get; set; }
        [Key(1)] public int Length { get; set; }
        [Key(2)] public BeltItemMessagePack Item { get; set; }
        [Key(3)] public bool Complete { get; set; }
        [SerializationConstructor]
        [Obsolete("For deserialization only.")] public BeltInsertionMessagePack() { }
        internal BeltInsertionMessagePack(BeltReplayInsertion value) { Complete = true; InputId = value.InputId; Length = value.Length; Item = new(value.Item); }
        internal BeltReplayInsertion Decode() => new(InputId, Length, Item.Decode());
    }
}
