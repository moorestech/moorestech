using Server.Util.MessagePack.BeltTransport;
using System;
using System.Collections.Generic;
using Core.BeltTransport;
namespace Client.Game.InGame.BeltTransport
{
    internal static class BeltTickStructureValidation
    {
        internal static string Validate(BeltTickMessagePack message)
        {
            if (message == null || message.BeforeTick == null || message.Outputs == null || message.AfterTick == null)
                return "Tick packet or stage arrays are missing.";
            // 全段階の構造を確認してから一度だけ共有状態へ変換する。
            // Validate every stage before converting the packet into shared state once.
            string reason = Changes(message.BeforeTick) ?? Changes(message.AfterTick);
            if (reason != null) return reason;
            foreach (var output in message.Outputs)
                if (output == null || !Direction(output.Direction) || output.Stage is not (3 or 4) ||
                    output.Offer < 0 || (!output.Succeeded && output.Offer == 0) || BeltConstants.ItemWidth < output.Offer || !Item(output.Item))
                    return "Invalid output acceptance.";
            return null;

            #region Internal
            string Changes(BeltChangeMessagePack[] changes)
            {
                foreach (var change in changes)
                {
                    switch (change)
                    {
                        case BeltInputChangeMessagePack input:
                            if (!Direction(input.Direction) || input.Length < 1 || BeltConstants.ItemWidth < input.Length || !Item(input.Item))
                                return "Invalid external input.";
                            break;
                        case BeltSpeedChangeMessagePack speed:
                            if (speed.Speeds == null) return "Speed array is missing.";
                            var speedIds = new HashSet<int>();
                            foreach (var value in speed.Speeds)
                                if (value == null || !Speed(value.Speed) || !speedIds.Add(value.CellId)) return "Invalid or duplicate speed cell.";
                            break;
                        case BeltCellItemsChangeMessagePack replacement:
                            if (!Items(replacement.Items)) return "Invalid cell replacement items.";
                            foreach (var item in replacement.Items)
                                if (item.CellId != replacement.CellId) return "Replacement item targets a different cell.";
                            break;
                        case BeltTopologyChangeMessagePack topology:
                            if (topology.ChangedCells == null || topology.RemovedCells == null || !Items(topology.AddedItems)) return "Invalid topology arrays.";
                            string connectionFailure = Connections(topology.AddedConnections) ?? Connections(topology.RemovedConnections);
                            if (connectionFailure != null) return connectionFailure;
                            var cellIds = new HashSet<int>();
                            foreach (var cell in topology.ChangedCells)
                                if (!Cell(cell) || !cellIds.Add(cell.Id)) return "Invalid or duplicate topology cell.";
                            var removedIds = new HashSet<int>();
                            foreach (int id in topology.RemovedCells)
                                if (!removedIds.Add(id) || cellIds.Contains(id)) return "Conflicting topology removal.";
                            break;
                        default: return "Missing or unknown boundary change.";
                    }
                }
                return null;

                #region Internal
                bool Items(BeltCellItemMessagePack[] items)
                {
                    if (items == null) return false;
                    var identities = new HashSet<Guid>();
                    foreach (var item in items)
                        if (item == null || !Item(item.Item) || !identities.Add(item.Item.Guid) || item.Progress < 1 ||
                            BeltConstants.ItemWidth < item.Progress || !Direction(item.EntryDirection) || item.EntryHeight < -1 || 1 < item.EntryHeight)
                            return false;
                    return true;
                }
                string Connections(BeltConnectionMessagePack[] connections)
                {
                    if (connections == null) return "Topology connections are missing.";
                    var seen = new HashSet<(int, int, bool, bool, BeltDirection, int)>();
                    var inputs = new Dictionary<int, HashSet<BeltDirection>>();
                    foreach (var edge in connections)
                    {
                        if (edge == null || !Direction(edge.Direction) || (!edge.SourceIsBelt && !edge.TargetIsBelt) ||
                            edge.EntryHeight < -1 || 1 < edge.EntryHeight) return "Invalid topology connection.";
                        // 各配列内の同一edgeだけを拒否し、追加と削除の相互一致は許す。
                        // Reject identical edges within each array while allowing an edge in both added and removed arrays.
                        if (!seen.Add((edge.SourceId, edge.TargetId, edge.SourceIsBelt, edge.TargetIsBelt, edge.Direction, edge.EntryHeight)))
                            return "Duplicate topology connection.";
                        // 実Attach対象は3方向までとし、同じ入力portを共有させない。
                        // Actual attachments allow three directions and cannot share an input port.
                        if (edge.SourceIsBelt && edge.TargetIsBelt)
                        {
                            if (!inputs.TryGetValue(edge.TargetId, out var ports)) inputs.Add(edge.TargetId, ports = new HashSet<BeltDirection>());
                            if (!ports.Add(edge.Direction)) return "Multiple sources share a belt input port.";
                            if (3 < ports.Count) return "A belt has more than three input ports.";
                        }
                    }
                    return null;
                }
                bool Cell(BeltCellMessagePack cell) => cell != null && Speed(cell.Speed) &&
                    !string.IsNullOrEmpty(cell.SpeedProfile) && Direction(cell.Forward) && cell.Surface != null &&
                    float.IsFinite(cell.Surface.InputHeight) && float.IsFinite(cell.Surface.OutputHeight);
                #endregion
            }
            bool Speed(int value) => 0 <= value && value <= BeltConstants.ItemWidth / 2;
            bool Direction(BeltDirection direction) => BeltDirection.Front <= direction && direction <= BeltDirection.Right;
            bool Item(BeltItemMessagePack item) => item != null && item.Guid != Guid.Empty && 0 < item.ItemId.AsPrimitive();
            #endregion
        }

    }
}
