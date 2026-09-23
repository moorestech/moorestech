using System;
using System.Collections.Generic;
using Core.Master;
using Game.BeltSegment;
namespace Server.Util.MessagePack.BeltSegment
{
    internal static class BeltWireValidation
    {
        internal const int MaximumCount = 1_048_576;
        internal static string Array<T>(T[] values) => values != null && values.Length <= MaximumCount ? null : "Missing or oversized array.";
        internal static string Index(int value, int count) => 0 <= value && value < count ? null : "ID outside generation.";
        internal static string Direction(BeltDirection value) => (uint)value < 4 ? null : "Invalid direction.";
        internal static string Speed(int value) => 0 <= value && value <= BeltConstants.ItemWidth / 2 ? null : "Invalid speed.";
        internal static string Item(BeltItemMessagePack item, HashSet<Guid> identities)
        {
            if (item == null || !item.Complete || item.Identity == Guid.Empty) return "Missing item identity.";
            if (!identities.Add(item.Identity)) return "Duplicate item identity.";
            if (item.Kind.AsPrimitive() <= 0 || !MasterHolder.ItemMaster.ExistItemId(item.Kind)) return "Unknown item kind.";
            if (Direction(item.AcceptedInput) is string direction) return direction;
            if (item.Position == null) return null;
            return item.Position.Complete && (uint)item.Position.Entry < 12 && 0 <= item.Position.Progress && item.Position.Progress <= BeltConstants.ItemWidth
                ? null : "Invalid detached position.";
        }
        internal static string RouteCell(BeltRouteCellMessagePack value)
            => value != null && value.Complete && (uint)value.Entry < 12 ? null : "Missing route cell or invalid entry.";
        internal static string UniqueIds(int[] values)
        {
            if (Array(values) is string reason) return reason;
            var ids = new HashSet<int>();
            foreach (int id in values)
                if (id < 0 || MaximumCount <= id || !ids.Add(id)) return "Invalid or repeated external ID.";
            return null;
        }
    }
}
