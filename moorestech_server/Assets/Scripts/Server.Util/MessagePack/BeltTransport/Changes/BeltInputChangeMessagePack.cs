using System;
using System.Linq;
using Core.BeltTransport;
using MessagePack;
namespace Server.Util.MessagePack.BeltTransport
{
    [MessagePackObject]
    public sealed class BeltInputChangeMessagePack : BeltChangeMessagePack
    {
        [Key(0)] public int CellId { get; }
        [Key(1)] public BeltDirection Direction { get; }
        [Key(2)] public int Length { get; }
        [Key(3)] public BeltItemMessagePack Item { get; }
        [SerializationConstructor]
        public BeltInputChangeMessagePack(int cellId, BeltDirection direction, int length, BeltItemMessagePack item)
        {
            CellId = cellId;
            Direction = direction;
            Length = length;
            Item = item;
        }
        [Obsolete("Reserved for MessagePack.")]
        public BeltInputChangeMessagePack() { }
        public BeltInputChangeMessagePack(BeltInputChange value)
        {
            CellId = value.CellId;
            Direction = value.Direction;
            Length = value.Length;
            Item = new(value.Item);
        }
        public override BeltBoundaryChange ToCore() => new BeltInputChange(CellId, Direction, Length, Item.ToCore());
    }
}
