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
        }
    }
}
