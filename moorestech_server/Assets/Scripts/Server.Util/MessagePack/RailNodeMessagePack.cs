using System;
using System.Collections.Generic;
using Game.Train.RailGraph;
using Game.Train.SaveLoad;
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
        [Key(0)] public Vector3IntMessagePack BlockPosition { get; set; }
        [Key(1)] public int ComponentIndex { get; set; }
        [Key(2)] public bool IsFrontSide { get; set; }

        [Obsolete("デシリアライズ用コンストラクタです。")]
        public ConnectionDestinationMessagePack()
        {
        }

        public ConnectionDestinationMessagePack(ConnectionDestination destination)
        {
            BlockPosition = new Vector3IntMessagePack(destination.blockPosition);
            ComponentIndex = destination.componentIndex;
            IsFrontSide = destination.IsFront;
        }

        public ConnectionDestination ToModel()
        {
            return new ConnectionDestination(BlockPosition.Vector3Int, ComponentIndex, IsFrontSide);
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
        [Key(6)] public long ServerTick { get; set; }

        [Obsolete("デシリアライズ用コンストラクタです。")]
        public RailNodeCreatedMessagePack()
        {
        }

        public RailNodeCreatedMessagePack(int nodeId, Guid nodeGuid, ConnectionDestination connectionDestination, Vector3 originPoint, Vector3 frontControlPoint, Vector3 backControlPoint, long serverTick)
        {
            NodeId = nodeId;
            NodeGuid = nodeGuid;
            ConnectionDestination = new ConnectionDestinationMessagePack(connectionDestination);
            OriginPoint = new Vector3MessagePack(originPoint);
            FrontControlPoint = new Vector3MessagePack(frontControlPoint);
            BackControlPoint = new Vector3MessagePack(backControlPoint);
            ServerTick = serverTick;
        }
    }

    /// <summary>
    ///     RailNode削除メッセージ差分を表す
    ///     Message payload for rail node removal diffs
    /// </summary>
    [MessagePackObject]
    public class RailNodeRemovedMessagePack
    {
        [Key(0)] public int NodeId { get; set; }
        [Key(1)] public Guid NodeGuid { get; set; }
        [Key(2)] public long ServerTick { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public RailNodeRemovedMessagePack()
        {
        }

        public RailNodeRemovedMessagePack(int nodeId, Guid nodeGuid, long serverTick)
        {
            NodeId = nodeId;
            NodeGuid = nodeGuid;
            ServerTick = serverTick;
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
        [Key(3)] public long GraphTick { get; set; }

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
                    node.BackControlPoint,
                    snapshot.GraphTick));
            }

            Connections = new List<RailGraphConnectionSnapshotMessagePack>(snapshot.Connections.Count);
            foreach (var connection in snapshot.Connections)
            {
                Connections.Add(new RailGraphConnectionSnapshotMessagePack(
                    connection.FromNodeId,
                    connection.ToNodeId,
                    connection.Distance,
                    connection.RailTypeGuid,
                    connection.IsDrawable));
            }

            GraphHash = snapshot.ConnectNodesHash;
            GraphTick = snapshot.GraphTick;
        }
    }

    [MessagePackObject]
    public class RailGraphConnectionSnapshotMessagePack
    {
        [Key(0)] public int FromNodeId { get; set; }
        [Key(1)] public int ToNodeId { get; set; }
        [Key(2)] public int Distance { get; set; }
        [Key(3)] public Guid RailTypeGuid { get; set; }
        [Key(4)] public bool IsDrawable { get; set; }

        [Obsolete("デシリアライズ用コンストラクタです。")]
        public RailGraphConnectionSnapshotMessagePack() { }

        public RailGraphConnectionSnapshotMessagePack(int fromNodeId, int toNodeId, int distance, Guid railTypeGuid, bool isDrawable)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            Distance = distance;
            RailTypeGuid = railTypeGuid;
            IsDrawable = isDrawable;
        }
    }

    /// <summary>
    ///     RailGraphのハッシュ状態通知に使用するメッセージ
    ///     Message payload for broadcasting RailGraph hash/tick state
    /// </summary>
    [MessagePackObject]
    public class RailGraphHashStateMessagePack
    {
        [Key(0)] public uint GraphHash { get; set; }
        [Key(1)] public long GraphTick { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public RailGraphHashStateMessagePack()
        {
        }

        public RailGraphHashStateMessagePack(uint graphHash, long graphTick)
        {
            GraphHash = graphHash;
            GraphTick = graphTick;
        }
    }
}
