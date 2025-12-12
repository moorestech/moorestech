using System;
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
}
