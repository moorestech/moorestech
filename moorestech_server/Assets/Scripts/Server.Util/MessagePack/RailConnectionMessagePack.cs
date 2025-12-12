using System;
using Game.Train.RailGraph;
using MessagePack;
using UnityEngine;

namespace Server.Util.MessagePack
{
    /// <summary>
    /// レール接続情報の共通MessagePackクラス
    /// Common MessagePack classes for rail connection information
    /// </summary>
    [MessagePackObject]
    public class RailConnectionDataMessagePack
    {
        [Key(0)] public RailNodeInfoMessagePack FromNode { get; set; }
        [Key(1)] public RailNodeInfoMessagePack ToNode { get; set; }
        [Key(2)] public int Distance { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RailConnectionDataMessagePack() { }
        
        public RailConnectionDataMessagePack(RailNodeInfoMessagePack from, RailNodeInfoMessagePack to, int distance)
        {
            FromNode = from;
            ToNode = to;
            Distance = distance;
        }
        
        /// <summary>
        /// RailConnectionInfoからRailConnectionDataを生成する静的メソッド（変換失敗時はnullを返す）
        /// Static method to create RailConnectionData from RailConnectionInfo (returns null on conversion failure)
        /// </summary>
        public static RailConnectionDataMessagePack TryCreate(RailConnectionInfo connectionInfo)
        {
            // FromNodeの情報を取得
            // Get FromNode information
            var fromConnectionDest = connectionInfo.FromNode.ConnectionDestination;
            if (fromConnectionDest.IsDefault()) return null;
            
            // ToNodeの情報を取得
            // Get ToNode information
            var toConnectionDest = connectionInfo.ToNode.ConnectionDestination;
            if (toConnectionDest.IsDefault()) return null;
            
            // RailControlPointを取得
            // Get RailControlPoint
            var fromControlPoint = fromConnectionDest.IsFront ? connectionInfo.FromNode.FrontControlPoint : connectionInfo.FromNode.BackControlPoint;
            var toControlPoint = toConnectionDest.IsFront ? connectionInfo.ToNode.FrontControlPoint : connectionInfo.ToNode.BackControlPoint;
            
            if (fromControlPoint == null || toControlPoint == null) return null;
            
            return new RailConnectionDataMessagePack(
                new RailNodeInfoMessagePack(connectionInfo.FromNode, fromConnectionDest),
                new RailNodeInfoMessagePack(connectionInfo.ToNode, toConnectionDest),
                connectionInfo.Distance
            );
        }
    }
    
    [MessagePackObject]
    public class RailNodeInfoMessagePack
    {
        [Key(0)] public RailComponentIDMessagePack ComponentId { get; set; }
        [Key(1)] public bool IsFrontSide { get; set; }
        [Key(2)] public RailControlPointMessagePack ControlPoint { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RailNodeInfoMessagePack() { }
        
        /// <summary>
        /// RailNodeとConnectionDestinationからRailNodeInfoを生成するコンストラクタ
        /// Constructor to create RailNodeInfo from RailNode and ConnectionDestination
        /// </summary>
        public RailNodeInfoMessagePack(RailNode node, ConnectionDestination connectionDestination)
        {
            // RailControlPointを取得
            // Get RailControlPoint
            var controlPoint = connectionDestination.IsFront ? node.FrontControlPoint : node.BackControlPoint;
            if (controlPoint == null)
            {
                throw new ArgumentException("RailControlPointが取得できませんでした");
            }
            
            ComponentId = new RailComponentIDMessagePack(connectionDestination.railComponentID.Position, connectionDestination.railComponentID.ID);
            IsFrontSide = connectionDestination.IsFront;
            ControlPoint = new RailControlPointMessagePack(controlPoint.OriginalPosition, controlPoint.ControlPointPosition);
        }
    }
    
    [MessagePackObject]
    public class RailControlPointMessagePack
    {
        [Key(0)] public Vector3MessagePack OriginalPosition { get; set; }
        [Key(1)] public Vector3MessagePack ControlPointPosition { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RailControlPointMessagePack() { }
        
        public RailControlPointMessagePack(Vector3 orig, Vector3 ctrl)
        {
            OriginalPosition = new Vector3MessagePack(orig);
            ControlPointPosition = new Vector3MessagePack(ctrl);
        }
    }
    
    [MessagePackObject]
    public class RailComponentIDMessagePack
    {
        [Key(0)] public Vector3IntMessagePack Position { get; set; }
        [Key(1)] public int ID { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RailComponentIDMessagePack() { }
        
        public RailComponentIDMessagePack(Vector3Int pos, int id)
        {
            Position = new Vector3IntMessagePack(pos);
            ID = id;
        }
        
        /// <summary>
        /// RailComponentIDからRailComponentIDMessagePackを生成するコンストラクタ
        /// Constructor to create RailComponentIDMessagePack from RailComponentID
        /// </summary>
        public RailComponentIDMessagePack(RailComponentID componentId)
        {
            Position = new Vector3IntMessagePack(componentId.Position);
            ID = componentId.ID;
        }
    }
}