using System.Linq;
using Core.BeltTransport;
using MessagePack;
namespace Server.Util.MessagePack.BeltTransport
{
    [MessagePackObject]
    public sealed class BeltSpeedChangeMessagePack : BeltChangeMessagePack
    {
        [Key(0)] public BeltSpeedMessagePack[] Speeds { get; }
        [SerializationConstructor]
        public BeltSpeedChangeMessagePack(BeltSpeedMessagePack[] speeds)
        {
            Speeds = speeds;
        }
        public BeltSpeedChangeMessagePack(BeltSpeedChange value)
        {
            Speeds = value.Speeds.Select(v => new BeltSpeedMessagePack(v)).ToArray();
        }
        internal override BeltBoundaryChange ToCore() => new BeltSpeedChange(Speeds.Select(v => v.ToCore()).ToArray());
    }
}
