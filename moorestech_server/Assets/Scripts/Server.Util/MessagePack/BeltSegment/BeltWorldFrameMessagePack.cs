using System;
using System.Linq;
using Core.Master;
using Game.BeltSegment;
using MessagePack;

namespace Server.Util.MessagePack.BeltSegment
{
    [MessagePackObject]
    public sealed class BeltWorldFrameMessagePack
    {
        [Key(0)] public ulong PreviousTick { get; set; }
        [Key(1)] public uint PreviousSequence { get; set; }
        [Key(2)] public ulong Tick { get; set; }
        [Key(3)] public uint Sequence { get; set; }
        [Key(4)] public ulong Generation { get; set; }
        [Key(5)] public uint PreviousHash { get; set; }
        [Key(6)] public BeltReplayTickMessagePack Replay { get; set; }
        [Key(7)] public bool Complete { get; set; }
        [SerializationConstructor]
        [Obsolete("For deserialization only.")] public BeltWorldFrameMessagePack() { }
        public BeltWorldFrameMessagePack(BeltWorldFrame value)
        { Complete = true;
            PreviousTick = value.Previous.Tick; PreviousSequence = value.Previous.Sequence;
            Tick = value.Position.Tick; Sequence = value.Position.Sequence; Generation = value.Generation;
            PreviousHash = value.PreviousHash; Replay = new(value.Replay);
        }
    }
}
