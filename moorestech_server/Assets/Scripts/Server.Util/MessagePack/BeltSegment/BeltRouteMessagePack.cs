using System;
using System.Linq;
using Core.Master;
using Game.BeltSegment;
using MessagePack;

namespace Server.Util.MessagePack.BeltSegment
{
    [MessagePackObject]
    public sealed class BeltRouteMessagePack
    {
        [Key(0)] public BeltRouteCellMessagePack[] Cells { get; set; }
        [Key(1)] public BeltRouteCellMessagePack[] EntryCells { get; set; }
        [Key(2)] public bool Complete { get; set; }
        [SerializationConstructor]
        [Obsolete("For deserialization only.")] public BeltRouteMessagePack() { }
        internal BeltRouteMessagePack(BeltRoute value)
        { Complete = true;
            Cells = value.Cells.Select(x => new BeltRouteCellMessagePack(x)).ToArray();
            EntryCells = value.EntryCells.Select(x => new BeltRouteCellMessagePack(x)).ToArray();
        }
        internal BeltRoute Decode() => new(Cells.Select(x => x.Decode()).ToArray(), EntryCells.Select(x => x.Decode()).ToArray());
    }
    [MessagePackObject]
    public sealed class BeltRouteCellMessagePack
    {
        [Key(0)] public int X { get; set; }
        [Key(1)] public int Y { get; set; }
        [Key(2)] public int Z { get; set; }
        [Key(3)] public BeltEntryDirection Entry { get; set; }
        [Key(4)] public int CenterHeightTwice { get; set; }
        [Key(5)] public int InputHeightTwice { get; set; }
        [Key(6)] public bool Complete { get; set; }
        [SerializationConstructor]
        [Obsolete("For deserialization only.")] public BeltRouteCellMessagePack() { }
        internal BeltRouteCellMessagePack(BeltRouteCell value)
        { Complete = true;
            X = value.Cell.X; Y = value.Cell.Y; Z = value.Cell.Z; Entry = value.Entry;
            CenterHeightTwice = value.CenterHeightTwice; InputHeightTwice = value.InputHeightTwice;
        }
        internal BeltRouteCell Decode() => new(new BeltCell(X, Y, Z), Entry, CenterHeightTwice, InputHeightTwice);
    }
}
