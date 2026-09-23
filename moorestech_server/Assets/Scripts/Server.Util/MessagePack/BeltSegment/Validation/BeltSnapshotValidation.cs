using System;
using System.Collections.Generic;
using Game.BeltSegment;
using static Server.Util.MessagePack.BeltSegment.BeltWireValidation;
namespace Server.Util.MessagePack.BeltSegment
{
    internal static class BeltSnapshotValidation
    {
        internal static string Validate(BeltWorldSnapshotMessagePack value)
        {
            if (!(value != null && value.Complete)) return "Missing snapshot.";
            if (Array(value.Segments) is string error1) return error1; if (Array(value.Links) is string error2) return error2; if (Array(value.Inputs) is string error3) return error3; if (Array(value.Outputs) is string error4) return error4; if (Array(value.Routes) is string error5) return error5;
            int count = value.Segments.Length;
            if (!(value.Routes.Length == count)) return "Route/segment mismatch.";
            var identities = new HashSet<Guid>();
            var cells = new HashSet<BeltCell>();
            long capacity = 0;
            for (int id = 0; id < count; id++)
            {
                var segment = value.Segments[id]; var route = value.Routes[id];
                if (!(segment != null && segment.Complete && route != null && route.Complete)) return "Missing segment or route.";
                if (!(0 < segment.Capacity && segment.Capacity <= MaximumCount)) return "Invalid capacity.";
                capacity += segment.Capacity;
                if (!(capacity <= MaximumCount)) return "Total capacity exceeds wire limit.";
                if (!((uint)segment.Kind <= (uint)BeltSegmentKind.Branch)) return "Invalid kind.";
                if (!(segment.Kind != BeltSegmentKind.Merge || segment.Capacity == 1)) return "Merge capacity must be one.";
                if (Speed(segment.Speed) is string error6) return error6; if (Array(segment.Items) is string error7) return error7; if (Array(route.Cells) is string error8) return error8; if (Array(route.EntryCells) is string error9) return error9;
                if (!(route.Cells.Length == segment.Capacity && route.EntryCells.Length == 4)) return "Invalid route lengths.";
                foreach (var cell in route.Cells)
                { if (RouteCell(cell) is string error10) return error10; if (!(cells.Add(new(cell.X, cell.Y, cell.Z)))) return "Duplicate route cell."; }
                foreach (var entry in route.EntryCells) if (RouteCell(entry) is string error11) return error11;
                if (!(segment.Items.Length <= segment.Capacity)) return "Queue overflow.";
                long previous = -BeltConstants.ItemWidth;
                foreach (var item in segment.Items)
                {
                    if (!(item != null && item.Complete && previous + BeltConstants.ItemWidth <= item.Distance && item.Distance < (long)segment.Capacity * BeltConstants.ItemWidth)) return "Invalid item spacing or distance.";
                    if (Item(item.Item, identities) is string error12) return error12; previous = item.Distance;
                }
                if (!(segment.Kind != BeltSegmentKind.Normal || (segment.BufferedItem == null && segment.PriorityIndex == 0))) return "Normal segment cannot hold junction state.";
                if (segment.BufferedItem != null) if (Item(segment.BufferedItem, identities) is string error13) return error13;
            }
            var inputs = new HashSet<(int, BeltDirection)>(); var outputs = new HashSet<(int, BeltDirection)>();
            var inputCounts = new int[count]; var outputCounts = new int[count];
            return ValidateWiring();
            #region Internal
            string ValidateWiring()
            {
                foreach (var link in value.Links)
                {
                    if (!(link != null && link.Complete)) return "Missing link.";
                    if (AddOutput(link.Source, link.Direction) is string error14) return error14; if (AddInput(link.Target, (BeltDirection)((int)link.Direction ^ 1)) is string error15) return error15;
                }
                foreach (var input in value.Inputs) { if (!(input != null && input.Complete)) return "Missing input."; if (AddInput(input.Target, input.Direction) is string error16) return error16; }
                foreach (var output in value.Outputs) { if (!(output != null && output.Complete)) return "Missing output."; if (AddOutput(output.Source, output.Direction) is string error17) return error17; }
                for (int id = 0; id < count; id++)
                {
                    var segment = value.Segments[id];
                    if (!(inputCounts[id] <= (segment.Kind == BeltSegmentKind.Merge ? 3 : 1))) return "Too many inputs.";
                    if (!(outputCounts[id] <= (segment.Kind == BeltSegmentKind.Branch ? 3 : 1))) return "Too many outputs.";
                    if (segment.Kind == BeltSegmentKind.Merge && inputCounts[id] < 2) return "Merge requires two or three inputs.";
                    if (segment.Kind == BeltSegmentKind.Branch && outputCounts[id] < 2) return "Branch requires two or three outputs.";
                    // topology縮小後もRR原値を保持し、最大2の探索offset加算だけを安全に制限する。
                    // Preserve raw RR after topology shrink; only bound addition of the maximum search offset of two.
                    if (!(0 <= segment.PriorityIndex && segment.PriorityIndex <= int.MaxValue - 2)) return "Invalid priority index.";
                }
                return null;
            }
            string AddInput(int id, BeltDirection direction)
            {
                if (Index(id, count) is string error) return error;
                if (Direction(direction) is string errorDirection) return errorDirection;
                if (!inputs.Add((id, direction))) return "Duplicate input port.";
                inputCounts[id]++; return null;
            }
            string AddOutput(int id, BeltDirection direction)
            {
                if (Index(id, count) is string error) return error;
                if (Direction(direction) is string errorDirection) return errorDirection;
                if (!outputs.Add((id, direction))) return "Duplicate output port.";
                outputCounts[id]++; return null;
            }
            #endregion
        }
    }
}
