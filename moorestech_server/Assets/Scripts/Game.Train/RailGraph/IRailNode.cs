using System;
using System.Collections.Generic;

namespace Game.Train.RailGraph
{
    // ノードのID・接続情報を共通で扱うためのインターフェイス
    // Interface to share node id and connection metadata between assemblies
    public interface IRailNode
    {
        int NodeId { get; }
        int OppositeNodeId { get; }
        IEnumerable<(int nodeId, int distance)> ConnectedNodesWithDistance { get; }
        int GetDistanceToNode(int nodeId, bool useFindPath);
        ConnectionDestination ConnectionDestination { get; }
        Guid NodeGuid { get; }
    }
}
