using System;
using System.Collections.Generic;

namespace Game.Train.RailGraph
{
    public static class RailGraphProvider
    {
        private static IRailGraphProvider _current = NullRailGraphProvider.Instance;
        public static IRailGraphProvider Current => _current;

        public static void SetProvider(IRailGraphProvider provider)
        {
            _current = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        private sealed class NullRailGraphProvider : IRailGraphProvider
        {
            public static readonly NullRailGraphProvider Instance = new NullRailGraphProvider();

            public IRailNode ResolveRailNode(ConnectionDestination destination) => null;

            public IReadOnlyList<IRailNode> FindShortestPath(IRailNode start, IRailNode end) => Array.Empty<IRailNode>();

            public int GetDistance(IRailNode start, IRailNode end, bool useFindPath) => -1;
        }
    }
}
