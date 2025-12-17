using Game.Train.RailGraph;
using Game.Train.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Client.Game.InGame.Train
{
    public class ClientRailNode : IRailNode
    {
        private int _nodeId;
        public int NodeId => _nodeId;
        private Guid _nodeGuid;
        public Guid NodeGuid => _nodeGuid;

        private ConnectionDestination _connectionDestination;
        public ConnectionDestination ConnectionDestination => _connectionDestination;

        private readonly RailGraphClientCache _cache;
        public int OppositeNodeId => NodeId ^ 1;
        public IRailNode OppositeNode
        {
            get
            {
                return _cache.TryGetNode(OppositeNodeId, out var irailnode) ? irailnode : null;
            }
        }
        public RailControlPoint FrontControlPoint { get; }
        public RailControlPoint BackControlPoint { get; }
        public StationReference StationRef => null;// TODO 今後適切に実装を
        public bool IsActive 
        { 
            get
            {
                return _cache != null && _cache.TryGetNode(NodeId, out var irailnode);
            }
        }
        
        public ClientRailNode(int nodeid, Guid guid, ConnectionDestination connectionDestination, Vector3 origin, Vector3 primary, Vector3 opposite,RailGraphClientCache cache)
        {
            _nodeId = nodeid;
            _nodeGuid = guid;
            _connectionDestination = connectionDestination;
            FrontControlPoint = new RailControlPoint(origin, primary);
            BackControlPoint = new RailControlPoint(origin, opposite);
            _cache = cache;
        }

        public IEnumerable<IRailNode> ConnectedNodes
        {
            get
            {
                var ret = new List<IRailNode>();
                var connectedNodeIds = _cache.ConnectNodes[NodeId];
                foreach (var (targetnodeId, _) in connectedNodeIds)
                {
                    ret.Add(_cache.Nodes[targetnodeId]);
                }
                return ret;
            }
        }

        public IEnumerable<(IRailNode node, int distance)> ConnectedNodesWithDistance
        {
            get
            {
                return _cache.ConnectNodes[NodeId].Select(x => (_cache.Nodes[x.Item1] as IRailNode, x.Item2)).ToList();
            }
        }

        public int GetDistanceToNode(IRailNode targetnode, bool useFindPath = false)
        {
            if (!IsActive || targetnode == null || targetnode.NodeId < 0) return -1;

            if (!useFindPath)
            {
                foreach (var (tempnode, distance) in ConnectedNodesWithDistance)
                {
                    if (tempnode.NodeId == targetnode.NodeId)
                        return distance;
                }
                return -1;
            }

            // Pathfinding logic
            var pathResult = _cache.FindShortestPath(NodeId, targetnode.NodeId);
            return RailNodeCalculate.CalculateTotalDistanceF(pathResult);
        }
    }

}


