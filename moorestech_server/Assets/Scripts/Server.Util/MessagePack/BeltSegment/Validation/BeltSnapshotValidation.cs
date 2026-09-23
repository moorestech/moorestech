using System;
using System.Collections.Generic;
using Game.BeltSegment;
using static Server.Util.MessagePack.BeltSegment.BeltWireValidation;
namespace Server.Util.MessagePack.BeltSegment
{
    internal static class BeltSnapshotValidation
    {
        internal static void Validate(BeltWorldSnapshotMessagePack value)
        {
            Require(value != null && value.Complete, "Missing snapshot.");
            Array(value.Segments); Array(value.Links); Array(value.Inputs); Array(value.Outputs); Array(value.Routes);
            int count = value.Segments.Length;
            Require(value.Routes.Length == count, "Route/segment mismatch.");
            var identities = new HashSet<Guid>();
            var cells = new HashSet<BeltCell>();
            long capacity = 0;
            for (int id = 0; id < count; id++)
            {
                var segment = value.Segments[id]; var route = value.Routes[id];
                Require(segment != null && segment.Complete && route != null && route.Complete, "Missing segment or route.");
                Require(segment.Capacity > 0 && segment.Capacity <= MaximumCount, "Invalid capacity.");
                capacity += segment.Capacity;
                Require(capacity <= MaximumCount, "Total capacity exceeds wire limit.");
                Require((uint)segment.Kind <= (uint)BeltSegmentKind.Branch, "Invalid kind.");
                Require(segment.Kind != BeltSegmentKind.Merge || segment.Capacity == 1, "Merge capacity must be one.");
                Speed(segment.Speed); Array(segment.Items); Array(route.Cells); Array(route.EntryCells);
                Require(route.Cells.Length == segment.Capacity && route.EntryCells.Length == 4, "Invalid route lengths.");
                foreach (var cell in route.Cells)
                { RouteCell(cell); Require(cells.Add(new(cell.X, cell.Y, cell.Z)), "Duplicate route cell."); }
                foreach (var entry in route.EntryCells) RouteCell(entry);
                Require(segment.Items.Length <= segment.Capacity, "Queue overflow.");
                long previous = -256;
                foreach (var item in segment.Items)
                {
                    Require(item != null && item.Complete && item.Distance >= previous + 256 && item.Distance < (long)segment.Capacity * 256,
                        "Invalid item spacing or distance.");
                    Item(item.Item, identities); previous = item.Distance;
                }
                Require(segment.Kind != BeltSegmentKind.Normal || (segment.BufferedItem == null && segment.PriorityIndex == 0),
                    "Normal segment cannot hold junction state.");
                if (segment.BufferedItem != null) Item(segment.BufferedItem, identities);
            }
            ValidateWiring(value);
        }
        private static void ValidateWiring(BeltWorldSnapshotMessagePack value)
        {
            int count = value.Segments.Length;
            var inputs = new HashSet<(int, BeltDirection)>(); var outputs = new HashSet<(int, BeltDirection)>();
            var inputCounts = new int[count]; var outputCounts = new int[count];
            foreach (var link in value.Links)
            {
                Require(link != null && link.Complete, "Missing link.");
                AddOutput(link.Source, link.Direction); AddInput(link.Target, (BeltDirection)((int)link.Direction ^ 1));
            }
            foreach (var input in value.Inputs) { Require(input != null && input.Complete, "Missing input."); AddInput(input.Target, input.Direction); }
            foreach (var output in value.Outputs) { Require(output != null && output.Complete, "Missing output."); AddOutput(output.Source, output.Direction); }
            for (int id = 0; id < count; id++)
            {
                var segment = value.Segments[id];
                Require(inputCounts[id] <= (segment.Kind == BeltSegmentKind.Merge ? 3 : 1), "Too many inputs.");
                Require(outputCounts[id] <= (segment.Kind == BeltSegmentKind.Branch ? 3 : 1), "Too many outputs.");
                // topology縮小後もRR原値を保持し、最大2の探索offset加算だけを安全に制限する。
                // Preserve raw RR after topology shrink; only bound addition of the maximum search offset of two.
                Require(segment.PriorityIndex >= 0 && segment.PriorityIndex <= int.MaxValue - 2, "Invalid priority index.");
            }
            #region Internal
            void AddInput(int id, BeltDirection direction)
            { Index(id, count); Direction(direction); Require(inputs.Add((id, direction)), "Duplicate input port."); inputCounts[id]++; }
            void AddOutput(int id, BeltDirection direction)
            { Index(id, count); Direction(direction); Require(outputs.Add((id, direction)), "Duplicate output port."); outputCounts[id]++; }
            #endregion
        }
    }
}
