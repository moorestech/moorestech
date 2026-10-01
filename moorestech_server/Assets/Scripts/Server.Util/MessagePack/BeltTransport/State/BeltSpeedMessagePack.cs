using System;
using Core.BeltTransport;
using MessagePack;

namespace Server.Util.MessagePack.BeltTransport
{
    [MessagePackObject]
    public sealed class BeltSpeedMessagePack
    {
        [Key(0)] public int CellId { get; }
        [Key(1)] public int Speed { get; }

        [SerializationConstructor]
        public BeltSpeedMessagePack(int cellId, int speed)
        {
            CellId = cellId;
            Speed = speed;
        }
        [Obsolete("Reserved for MessagePack.")]
        public BeltSpeedMessagePack() { }

        public BeltSpeedMessagePack(BeltCellSpeed value)
        {
            CellId = value.CellId;
            Speed = value.Speed;
        }

        public BeltCellSpeed ToCore() => new(CellId, Speed);
    }
}
