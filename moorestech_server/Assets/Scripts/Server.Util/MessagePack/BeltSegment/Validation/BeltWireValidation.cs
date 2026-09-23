using System;
using System.Collections.Generic;
using Core.Master;
using Game.BeltSegment;
namespace Server.Util.MessagePack.BeltSegment
{
    internal static class BeltWireValidation
    {
        // ワイヤ上限はGPU列合計と外部配列の割当てを制限する。
        // Wire limits bound total GPU lanes and external-array allocations.
        // DTO末尾のCompleteが欠けた配列を拒否し、必須scalarの欠損を0で補わない。
        // Reject arrays missing the final Complete marker instead of defaulting required scalars to zero.
        internal const int MaximumCount = 1_048_576;
        internal static void Require(bool condition, string reason)
        { if (!condition) throw new ArgumentException($"Invalid belt payload: {reason}"); }
        internal static void Array<T>(T[] values)
            => Require(values != null && values.Length <= MaximumCount, "Missing or oversized array.");
        internal static void Index(int value, int count) => Require(value >= 0 && value < count, "ID outside generation.");
        internal static void Direction(BeltDirection value) => Require((uint)value < 4, "Invalid direction.");
        internal static void Speed(int value) => Require(value >= 0 && value <= BeltConstants.ItemWidth / 2, "Invalid speed.");
        internal static void Item(BeltItemMessagePack item, HashSet<Guid> identities)
        {
            Require(item != null && item.Complete && item.Identity != Guid.Empty, "Missing item identity.");
            Require(identities.Add(item.Identity), "Duplicate item identity.");
            Require(item.Kind.AsPrimitive() > 0, "Empty item kind.");
            MasterHolder.ItemMaster.GetItemMaster(item.Kind);
            Direction(item.AcceptedInput);
            if (item.Position == null) return;
            Require(item.Position.Complete && (uint)item.Position.Entry < 12 && item.Position.Progress >= 0 && item.Position.Progress <= 256,
                "Invalid detached position.");
        }
        internal static void RouteCell(BeltRouteCellMessagePack value)
            => Require(value != null && value.Complete && (uint)value.Entry < 12, "Missing route cell or invalid entry.");
        internal static void UniqueIds(int[] values)
        {
            Array(values);
            var ids = new HashSet<int>();
            foreach (int id in values) Require(id >= 0 && id < MaximumCount && ids.Add(id), "Invalid or repeated external ID.");
        }
    }
}
