using System.Collections.Generic;

namespace Game.Train.RailGraph
{
    public interface IRailGraphProvider
    {
        IRailNode ResolveRailNode(ConnectionDestination destination);
        IReadOnlyList<IRailNode> FindShortestPath(IRailNode start, IRailNode end);
    }
}
