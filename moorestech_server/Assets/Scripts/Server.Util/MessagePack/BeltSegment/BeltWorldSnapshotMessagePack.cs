using System;
using System.Linq;
using Core.Master;
using Game.BeltSegment;
using MessagePack;

namespace Server.Util.MessagePack.BeltSegment
{
    [MessagePackObject]
    public sealed class BeltWorldSnapshotMessagePack
    {
        [Key(0)] public ulong Tick { get; set; }
        [Key(1)] public uint Sequence { get; set; }
        [Key(2)] public ulong Generation { get; set; }
        [Key(3)] public BeltReplayStateMessagePack[] Segments { get; set; }
        [Key(4)] public BeltLinkMessagePack[] Links { get; set; }
        [Key(5)] public BeltInputMessagePack[] Inputs { get; set; }
        [Key(6)] public BeltOutputMessagePack[] Outputs { get; set; }
        [Key(7)] public BeltRouteMessagePack[] Routes { get; set; }
        [Key(8)] public bool Complete { get; set; }
        [SerializationConstructor]
        [Obsolete("For deserialization only.")] public BeltWorldSnapshotMessagePack() { }
        public BeltWorldSnapshotMessagePack(BeltWorldSnapshot value)
        { Complete = true;
            Tick = value.Position.Tick; Sequence = value.Position.Sequence; Generation = value.Generation;
            Segments = value.Simulation.Segments.Select(x => new BeltReplayStateMessagePack(x)).ToArray();
            Links = value.Simulation.Links.Select(x => new BeltLinkMessagePack(x)).ToArray();
            Inputs = value.Simulation.Inputs.Select(x => new BeltInputMessagePack(x)).ToArray();
            Outputs = value.Simulation.Outputs.Select(x => new BeltOutputMessagePack(x)).ToArray();
            Routes = value.Routes.Select(x => new BeltRouteMessagePack(x)).ToArray();
        }
    }
}
