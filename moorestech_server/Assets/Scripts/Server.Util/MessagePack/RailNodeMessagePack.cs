using System;
using System.Collections.Generic;
using Game.Train.RailGraph;
using MessagePack;
using UnityEngine;

namespace Server.Util.MessagePack
{
    /// <summary>
    ///     ConnectionDestinationのシリアライズ表現
    ///     MessagePack representation of ConnectionDestination
    /// </summary>
    [MessagePackObject]
    public class ConnectionDestinationMessagePack
    {
        [Key(0)] public RailComponentIDMessagePack ComponentId { get; set; }
        [Key(1)] public bool IsFrontSide { get; set; }

        [Obsolete("デシリアライズ用コンストラクタです。")]
        public ConnectionDestinationMessagePack()
        {
        }

        public ConnectionDestinationMessagePack(ConnectionDestination destination)
        {
            ComponentId = new RailComponentIDMessagePack(destination.railComponentID);
            IsFrontSide = destination.IsFront;
        }

        public ConnectionDestination ToModel()
        {
            var componentId = new RailComponentID(ComponentId.Position.Vector3Int, ComponentId.ID);
            return new ConnectionDestination(componentId, IsFrontSide);
        }
    }

    /// <summary>
    ///     RailNode生成時の通知ペイロード
    ///     Message payload for rail node creation
    /// </summary>
    [MessagePackObject]
    public class RailNodeCreatedMessagePack
    {
        [Key(0)] public int NodeId { get; set; }
        [Key(1)] public Guid NodeGuid { get; set; }
        [Key(2)] public ConnectionDestinationMessagePack ConnectionDestination { get; set; }
        [Key(3)] public Vector3MessagePack OriginPoint { get; set; }
        [Key(4)] public Vector3MessagePack FrontControlPoint { get; set; }
        [Key(5)] public Vector3MessagePack BackControlPoint { get; set; }

        [Obsolete("デシリアライズ用コンストラクタです。")]
        public RailNodeCreatedMessagePack()
        {
        }

        public RailNodeCreatedMessagePack(int nodeId, Guid nodeGuid, ConnectionDestination connectionDestination, Vector3 originPoint, Vector3 frontControlPoint, Vector3 backControlPoint)
        {
            NodeId = nodeId;
            NodeGuid = nodeGuid;
            ConnectionDestination = new ConnectionDestinationMessagePack(connectionDestination);
            OriginPoint = new Vector3MessagePack(originPoint);
            FrontControlPoint = new Vector3MessagePack(frontControlPoint);
            BackControlPoint = new Vector3MessagePack(backControlPoint);
        }
    }

    /// <summary>
    ///     RailNode蜿門ｾ励ゅ′繝｡繝・そ繝ｼ繧ｸ蜑･螟悶＠縺ｦ縺�・
    ///     Message payload for rail node removal diffs
    /// </summary>
    [MessagePackObject]
    public class RailNodeRemovedMessagePack
    {
        [Key(0)] public int NodeId { get; set; }
        [Key(1)] public Guid NodeGuid { get; set; }

        [Obsolete("繝・す繝ｪ繧｢繝ｩ繧､繧ｺ逕ｨ縺ｮ繧ｳ繝ｳ繧ｹ繝医Λ繧ｯ繧ｿ縺ｧ縺吶・")]
        public RailNodeRemovedMessagePack()
        {
        }

        public RailNodeRemovedMessagePack(int nodeId, Guid nodeGuid)
        {
            NodeId = nodeId;
            NodeGuid = nodeGuid;
        }
    }

    /// <summary>
    ///     RailGraph全体のスナップショット
    ///     Snapshot payload that contains all rail nodes and connections
    /// </summary>
    [MessagePackObject]
    public class RailGraphSnapshotMessagePack
    {
        [Key(0)] public List<RailNodeCreatedMessagePack> Nodes { get; set; }
        [Key(1)] public List<RailGraphConnectionSnapshotMessagePack> Connections { get; set; }
        [Key(2)] public uint GraphHash { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。")]
        public RailGraphSnapshotMessagePack() { }

        public RailGraphSnapshotMessagePack(RailGraphSnapshot snapshot)
        {
            Nodes = new List<RailNodeCreatedMessagePack>(snapshot.Nodes.Count);
            foreach (var node in snapshot.Nodes)
            {
                Nodes.Add(new RailNodeCreatedMessagePack(
                    node.NodeId,
                    node.NodeGuid,
                    node.ConnectionDestination,
                    node.OriginPoint,
                    node.FrontControlPoint,
                    node.BackControlPoint));
            }

            Connections = new List<RailGraphConnectionSnapshotMessagePack>(snapshot.Connections.Count);
            foreach (var connection in snapshot.Connections)
            {
                Connections.Add(new RailGraphConnectionSnapshotMessagePack(
                    connection.FromNodeId,
                    connection.ToNodeId,
                    connection.Distance));
            }

            GraphHash = snapshot.ConnectNodesHash;
        }
    }

    [MessagePackObject]
    public class RailGraphConnectionSnapshotMessagePack
    {
        [Key(0)] public int FromNodeId { get; set; }
        [Key(1)] public int ToNodeId { get; set; }
        [Key(2)] public int Distance { get; set; }

        [Obsolete("デシリアライズ用コンストラクタです。")]
        public RailGraphConnectionSnapshotMessagePack() { }

        public RailGraphConnectionSnapshotMessagePack(int fromNodeId, int toNodeId, int distance)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            Distance = distance;
        }
    }
}
