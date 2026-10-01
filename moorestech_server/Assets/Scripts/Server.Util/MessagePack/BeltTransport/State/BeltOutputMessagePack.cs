using System;
using Core.BeltTransport;
using MessagePack;
namespace Server.Util.MessagePack.BeltTransport
{
    [MessagePackObject]
    public sealed class BeltOutputMessagePack
    {
        [Key(0)] public int SourceCellId { get; }
        [Key(1)] public int TargetId { get; }
        [Key(2)] public int Stage { get; }
        [Key(3)] public BeltDirection Direction { get; }
        [Key(4)] public int Offer { get; }
        [Key(5)] public bool Succeeded { get; }
        [Key(6)] public BeltItemMessagePack Item { get; }
        [SerializationConstructor]
        public BeltOutputMessagePack(int sourceCellId, int targetId, int stage, BeltDirection direction, int offer, bool succeeded, BeltItemMessagePack item)
        {
            SourceCellId = sourceCellId;
            TargetId = targetId;
            Stage = stage;
            Direction = direction;
            Offer = offer;
            Succeeded = succeeded;
            Item = item;
        }
        [Obsolete("Reserved for MessagePack.")]
        public BeltOutputMessagePack() { }
        public BeltOutputMessagePack(BeltOutputResult value)
        {
            SourceCellId = value.SourceCellId; TargetId = value.TargetId; Stage = value.Stage;
            Direction = value.Direction; Offer = value.Offer; Succeeded = value.Succeeded; Item = new(value.Item);
        }
        public BeltOutputResult ToCore() => new(SourceCellId, TargetId, Stage, Direction, Offer, Succeeded, Item.ToCore());
    }
}
