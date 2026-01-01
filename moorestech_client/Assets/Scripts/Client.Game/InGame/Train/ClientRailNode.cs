using Game.Train.RailGraph;
using Game.Train.Utility;
using System;
using System.Collections.Generic;
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
        private StationReference _stationRef;
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
        public StationReference StationRef => _stationRef;
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
            _stationRef = new StationReference();
        }

        public void UpdateStationReference(StationReference stationReference)
        {
            // 駅参照を更新する
            // Update station reference.
            _stationRef = stationReference ?? new StationReference();
        }

        public IEnumerable<IRailNode> ConnectedNodes
        {
            get
            {
                foreach (var (targetnodeId, _) in _cache.ConnectNodes[NodeId])
                {
                    var target = _cache.Nodes[targetnodeId];
                    if (target != null)
                        yield return target;
                }
            }
        }

        public IEnumerable<(IRailNode node, int distance)> ConnectedNodesWithDistance
        {
            get
            {
                foreach (var (targetnodeId, distance) in _cache.ConnectNodes[NodeId])
                {
                    var target = _cache.Nodes[targetnodeId];
                    if (target != null)
                        yield return (target, distance);
                }
            }
        }

        public int GetDistanceToNode(IRailNode targetnode, bool useFindPath = false)
        {
            return RailGraphProvider.Current.GetDistance(this, targetnode, useFindPath);
        }
    }
}
