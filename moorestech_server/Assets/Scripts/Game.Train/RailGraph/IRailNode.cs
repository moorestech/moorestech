using System;
using System.Collections.Generic;
using Game.Train.SaveLoad;

namespace Game.Train.RailGraph
{
    // ノードのID・接続情報を共通で扱うためのインターフェイス
    // Interface to share node id and connection metadata between assemblies
    public interface IRailNode
    {
        int NodeId { get; }
        int OppositeNodeId { get; }
        IRailNode OppositeNode { get; }
        ConnectionDestination ConnectionDestination { get; }
        Guid NodeGuid { get; }
        IRailGraphProvider GraphProvider { get; }
        StationReference StationRef { get; }
        RailControlPoint FrontControlPoint { get; }
        RailControlPoint BackControlPoint { get; }
        IEnumerable<IRailNode> ConnectedNodes { get; }
        IEnumerable<(IRailNode node, int distance)> ConnectedNodesWithDistance { get; }
        int GetDistanceToNode(IRailNode node, bool useFindPath = false);
    }
}
