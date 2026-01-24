using System.Collections.Generic;
using Game.Train.SaveLoad;

namespace Game.Train.RailGraph
{
    public interface IRailGraphProvider
    {
        IRailNode ResolveRailNode(ConnectionDestination destination);
        IReadOnlyList<IRailNode> FindShortestPath(IRailNode start, IRailNode end);
        int GetDistance(IRailNode start, IRailNode end, bool useFindPath);
    }
}
