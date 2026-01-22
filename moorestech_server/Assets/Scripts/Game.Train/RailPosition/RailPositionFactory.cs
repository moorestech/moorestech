using Game.Train.RailGraph;
using System;
using System.Collections.Generic;

namespace Game.Train.RailPosition
{
    public static class RailPositionFactory
    {
        public static RailPosition Restore(RailPositionSaveData saveData, IRailGraphProvider railGraphProvider)
        {
            if (saveData == null)
            {
                return null;
            }

            // スナップショットをプロバイダで解決する
            // Resolve snapshot nodes via the provider
            var nodes = ResolveNodes(saveData.RailSnapshot, railGraphProvider);
            if (nodes.Count == 0)
            {
                return null;
            }

            var trainLength = Math.Max(0, saveData.TrainLength);
            var distanceToNextNode = Math.Max(0, saveData.DistanceToNextNode);
            return new RailPosition(nodes, trainLength, distanceToNextNode);
        }

        public static List<IRailNode> ResolveNodes(IEnumerable<ConnectionDestination> snapshot, IRailGraphProvider railGraphProvider)
        {
            var nodes = new List<IRailNode>();
            if (snapshot == null)
            {
                return nodes;
            }

            foreach (var destination in snapshot)
            {
                var node = railGraphProvider.ResolveRailNode(destination);
                if (node != null)
                {
                    nodes.Add(node);
                }
            }

            return nodes;
        }
    }
}
