using System;
using System.Linq;
using Core.Master;
using Game.BeltSegment;
using MessagePack;

namespace Server.Util.MessagePack.BeltSegment
{
    [MessagePackObject]
    public sealed class BeltLinkMessagePack
    {
        [Key(0)] public int Source { get; set; }
        [Key(1)] public int Target { get; set; }
        [Key(2)] public BeltDirection Direction { get; set; }
        [Key(3)] public bool Complete { get; set; }
        [SerializationConstructor]
        [Obsolete("For deserialization only.")] public BeltLinkMessagePack() { }
        internal BeltLinkMessagePack(BeltReplayLink value) { Complete = true; Source = value.SourceSegmentId; Target = value.TargetSegmentId; Direction = value.OutputDirection; }
        internal BeltReplayLink Decode() => new(Source, Target, Direction);
    }
    [MessagePackObject]
    public sealed class BeltInputMessagePack
    {
        [Key(0)] public int Target { get; set; }
        [Key(1)] public BeltDirection Direction { get; set; }
        [Key(2)] public bool Complete { get; set; }
        [SerializationConstructor]
        [Obsolete("For deserialization only.")] public BeltInputMessagePack() { }
        internal BeltInputMessagePack(BeltReplayInput value) { Complete = true; Target = value.TargetSegmentId; Direction = value.InputDirection; }
        internal BeltReplayInput Decode() => new(Target, Direction);
    }
    [MessagePackObject]
    public sealed class BeltOutputMessagePack
    {
        [Key(0)] public int Source { get; set; }
        [Key(1)] public BeltDirection Direction { get; set; }
        [Key(2)] public bool Complete { get; set; }
        [SerializationConstructor]
        [Obsolete("For deserialization only.")] public BeltOutputMessagePack() { }
        internal BeltOutputMessagePack(BeltReplayOutput value) { Complete = true; Source = value.SourceSegmentId; Direction = value.OutputDirection; }
        internal BeltReplayOutput Decode() => new(Source, Direction);
    }
}
