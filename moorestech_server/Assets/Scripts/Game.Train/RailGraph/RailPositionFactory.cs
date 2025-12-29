using Game.Train.Train;
using System;
using System.Collections.Generic;

namespace Game.Train.RailGraph
{
    public static class RailPositionFactory
    {
        public static RailPosition Restore(RailPositionSaveData saveData)
        {
            if (saveData == null)
            {
                return null;
            }

            var nodes = ResolveNodes(saveData.RailSnapshot);
            if (nodes.Count == 0)
            {
                return null;
            }

            var trainLength = Math.Max(0, saveData.TrainLength);
            var distanceToNextNode = Math.Max(0, saveData.DistanceToNextNode);
            return new RailPosition(nodes, trainLength, distanceToNextNode);
        }

        public static List<IRailNode> ResolveNodes(IEnumerable<ConnectionDestination> snapshot)
        {
            var nodes = new List<IRailNode>();
            if (snapshot == null)
            {
                return nodes;
            }

            foreach (var destination in snapshot)
            {
                var node = RailGraphProvider.Current.ResolveRailNode(destination);
                if (node != null)
                {
                    nodes.Add(node);
                }
            }

            return nodes;
        }
    }
}
