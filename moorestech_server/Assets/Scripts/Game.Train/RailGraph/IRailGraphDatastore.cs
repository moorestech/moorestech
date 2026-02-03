using System;
using System.Collections.Generic;
using Game.Train.SaveLoad;
using UnityEngine;

namespace Game.Train.RailGraph
{
    public interface IRailGraphDatastore : IRailGraphProvider
    {
        IObservable<RailNodeInitializationData> GetRailNodeInitializedEvent();
        IObservable<RailConnectionInitializationData> GetRailConnectionInitializedEvent();
        IObservable<RailNodeRemovedData> GetRailNodeRemovedEvent();
        IObservable<RailConnectionRemovalData> GetRailConnectionRemovedEvent();
        Dictionary<Vector3Int, (ConnectionDestination first, ConnectionDestination second)> GetRailPositionToConnectionDestination();
        void AddNodeSingle(RailNode node);
        void AddNodePair(RailNode node1, RailNode node2);
        void ConnectNode(RailNode node, RailNode targetNode, int distance, Guid railTypeGuid, bool isDrawable);
        void DisconnectNode(RailNode node, RailNode targetNode);
        List<(IRailNode, int)> GetConnectedNodesWithDistance(IRailNode node);
        void RemoveNode(RailNode node);
        List<IRailNode> FindShortestPath(IRailNode start, IRailNode target);
        List<IRailNode> FindShortestPath(int startId, int targetId);
        int GetDistanceBetweenNodes(int startId, int targetId);
        int GetDistanceBetweenNodes(IRailNode start, IRailNode target);
        bool TryGetRailNodeId(RailNode node, out int nodeId);
        bool TryGetRailNode(int nodeId, out RailNode railNode);
        IReadOnlyCollection<RailSegment> GetRailSegments();
        bool TryRestoreRailSegment(ConnectionDestination start, ConnectionDestination end, int length, Guid railTypeGuid, bool isDrawable);
        bool TryGetRailSegmentType(int startNodeId, int endNodeId, out Guid railTypeGuid);
        uint GetConnectNodesHash();
        RailGraphSnapshot CaptureSnapshot(long currentTick);
        IReadOnlyList<RailNode> GetRailNodes();
        void Reset();
    }
}
