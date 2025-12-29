using Game.Train.Utility;
using System;
using System.Collections.Generic;

namespace Game.Train.RailGraph
{
    public static class RailGraphProvider
    {
        private static IRailGraphProvider _current = new RailGraphDatastoreProxy();
        public static IRailGraphProvider Current => _current;

        public static void SetProvider(IRailGraphProvider provider)
        {
            _current = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private sealed class RailGraphDatastoreProxy : IRailGraphProvider
        {
            public IRailNode ResolveRailNode(ConnectionDestination destination)
            {
                return RailGraphDatastore.ResolveRailNode(destination);
            }

            public IReadOnlyList<IRailNode> FindShortestPath(IRailNode start, IRailNode end)
            {
                return RailGraphDatastore.FindShortestPath(start, end);
            }

            public int GetDistance(IRailNode start, IRailNode end, bool useFindPath)
            {
                if (start == null || end == null)
                {
                    return -1;
                }

                if (!useFindPath)
                {
                    return RailGraphDatastore.GetDistanceBetweenNodes(start, end);
                }

                var path = RailGraphDatastore.FindShortestPath(start, end);
                return RailNodeCalculate.CalculateTotalDistanceF(path);
            }
        }
    }
}
