using System;
using UnityEngine;
using Game.Train.SaveLoad;

namespace Game.Train.RailGraph
{
    // レールグラフ通知用のデータ構造
    // Data structures for rail graph notifications
    public readonly struct RailNodeInitializationData
    {
        public RailNodeInitializationData(int nodeId, Guid nodeGuid, ConnectionDestination connectionDestination, Vector3 originPoint, Vector3 frontControlPoint, Vector3 backControlPoint)
        {
            NodeId = nodeId;
            NodeGuid = nodeGuid;
            ConnectionDestination = connectionDestination;
            OriginPoint = originPoint;
            FrontControlPoint = frontControlPoint;
            BackControlPoint = backControlPoint;
        }

        public int NodeId { get; }
        public Guid NodeGuid { get; }
        public ConnectionDestination ConnectionDestination { get; }
        public Vector3 OriginPoint { get; }
        public Vector3 FrontControlPoint { get; }
        public Vector3 BackControlPoint { get; }
    }

    public readonly struct RailConnectionInitializationData
    {
        public RailConnectionInitializationData(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid, int distance, Guid railTypeGuid, bool isDrawable)
        {
            FromNodeId = fromNodeId;
            FromGuid = fromGuid;
            ToNodeId = toNodeId;
            ToGuid = toGuid;
            Distance = distance;
            RailTypeGuid = railTypeGuid;
            IsDrawable = isDrawable;
        }

        public int FromNodeId { get; }
        public Guid FromGuid { get; }
        public int ToNodeId { get; }
        public Guid ToGuid { get; }
        public int Distance { get; }
        public Guid RailTypeGuid { get; }
        public bool IsDrawable { get; }
    }

    public readonly struct RailNodeRemovedData
    {
        public RailNodeRemovedData(int nodeId, Guid nodeGuid)
        {
            NodeId = nodeId;
            NodeGuid = nodeGuid;
        }

        public int NodeId { get; }
        public Guid NodeGuid { get; }
    }

    public readonly struct RailConnectionRemovalData
    {
        public RailConnectionRemovalData(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid)
        {
            FromNodeId = fromNodeId;
            FromGuid = fromGuid;
            ToNodeId = toNodeId;
            ToGuid = toGuid;
        }

        public int FromNodeId { get; }
        public Guid FromGuid { get; }
        public int ToNodeId { get; }
        public Guid ToGuid { get; }
    }
}
