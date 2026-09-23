using System;
using System.Linq;
using Core.Master;
using Game.BeltSegment;
using MessagePack;

namespace Server.Util.MessagePack.BeltSegment
{
    [MessagePackObject]
    public sealed class BeltItemMessagePack
    {
        [Key(0)] public Guid Identity { get; set; }
        [Key(1)] public ItemId Kind { get; set; }
        [Key(2)] public BeltDirection AcceptedInput { get; set; }
        [Key(3)] public BeltPositionMessagePack Position { get; set; }
        [Key(4)] public bool Complete { get; set; }
        [SerializationConstructor]
        [Obsolete("For deserialization only.")] public BeltItemMessagePack() { }
        internal BeltItemMessagePack(BeltItem value)
        { Complete = true;
            Identity = value.Guid; Kind = new ItemId(value.ItemId); AcceptedInput = value.AcceptedInput;
            // 呼出元所有の位置を値へコピーし、以後の更新から分離する。
            // Copy caller-owned position values to detach them from subsequent updates.
            Position = value.Position == null ? null : new BeltPositionMessagePack(value.Position);
        }
        internal BeltItem Decode() => new() { Guid = Identity, ItemId = Kind.AsPrimitive(),
            AcceptedInput = AcceptedInput, Position = Position?.Decode() };
    }
    [MessagePackObject]
    public sealed class BeltPositionMessagePack
    {
        [Key(0)] public int X { get; set; }
        [Key(1)] public int Y { get; set; }
        [Key(2)] public int Z { get; set; }
        [Key(3)] public BeltEntryDirection Entry { get; set; }
        [Key(4)] public int Progress { get; set; }
        [Key(5)] public bool Complete { get; set; }
        [SerializationConstructor]
        [Obsolete("For deserialization only.")] public BeltPositionMessagePack() { }
        internal BeltPositionMessagePack(ItemPosition value)
        { Complete = true; X = value.CurrentCell.X; Y = value.CurrentCell.Y; Z = value.CurrentCell.Z; Entry = value.EntryDirection; Progress = value.Progress; }
        internal ItemPosition Decode() => new(new BeltCell(X, Y, Z), Entry, Progress);
    }
    [MessagePackObject]
    public sealed class BeltItemStateMessagePack
    {
        [Key(0)] public BeltItemMessagePack Item { get; set; }
        [Key(1)] public int Distance { get; set; }
        [Key(2)] public bool Complete { get; set; }
        [SerializationConstructor]
        [Obsolete("For deserialization only.")] public BeltItemStateMessagePack() { }
        internal BeltItemStateMessagePack(BeltItemState value) { Complete = true; Item = new(value.Item); Distance = value.DistanceToExit; }
        internal BeltItemState Decode() => new(Item.Decode(), Distance);
    }
}
