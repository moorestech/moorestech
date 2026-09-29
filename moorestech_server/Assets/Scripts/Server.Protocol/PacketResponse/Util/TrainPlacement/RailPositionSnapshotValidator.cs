using System.Collections.Generic;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.Util.TrainPlacement
{
    // 車両設置2経路で同じレール位置検証を共有する
    // Share rail-position validation between both train placement paths
    internal static class RailPositionSnapshotValidator
    {
        internal static bool TryValidate(RailPositionSnapshotMessagePack snapshot, int expectedLength, IRailGraphDatastore graph,
            out RailPosition position, out bool railNotFound)
        {
            position = null;
            railNotFound = false;
            if (snapshot == null) return false;
            var saveData = snapshot.ToModel();
            if (saveData == null || saveData.RailSnapshot == null || saveData.RailSnapshot.Count < 2 ||
                saveData.TrainLength != expectedLength || saveData.DistanceToNextNode < 0) return false;

            // ノード列を解決し、欠損と不連結を区別する
            // Resolve nodes and distinguish missing rails from disconnected paths
            var nodes = new List<IRailNode>(saveData.RailSnapshot.Count);
            foreach (var destination in saveData.RailSnapshot)
            {
                var node = graph.ResolveRailNode(destination);
                if (node == null)
                {
                    railNotFound = true;
                    return false;
                }
                nodes.Add(node);
            }

            var totalDistance = 0;
            for (var i = 0; i < nodes.Count - 1; i++)
            {
                var segmentDistance = nodes[i + 1].GetDistanceToNode(nodes[i]);
                if (segmentDistance <= 0 || i == 0 && segmentDistance < saveData.DistanceToNextNode) return false;
                totalDistance += segmentDistance;
            }
            if (totalDistance < saveData.TrainLength + saveData.DistanceToNextNode) return false;

            var validated = new RailPositionSaveData
            {
                TrainLength = expectedLength,
                DistanceToNextNode = saveData.DistanceToNextNode,
                RailSnapshot = saveData.RailSnapshot,
            };
            position = RailPositionFactory.Restore(validated, graph);
            return position != null;
        }
    }
}
