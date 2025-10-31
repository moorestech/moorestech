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
    public static class RailConnectionMessagePack
    {
        [MessagePackObject]
        public class RailConnectionData
        {
            [Key(0)] public RailNodeInfo FromNode { get; set; }
            [Key(1)] public RailNodeInfo ToNode { get; set; }
            [Key(2)] public int Distance { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectionData() { }
            
            public RailConnectionData(RailNodeInfo from, RailNodeInfo to, int distance)
            {
                FromNode = from;
                ToNode = to;
                Distance = distance;
            }
            
            /// <summary>
            /// RailConnectionInfoからRailConnectionDataを生成する静的メソッド（変換失敗時はnullを返す）
            /// Static method to create RailConnectionData from RailConnectionInfo (returns null on conversion failure)
            /// </summary>
            public static RailConnectionData TryCreate(RailConnectionInfo connectionInfo)
            {
                // FromNodeの情報を取得
                // Get FromNode information
                var fromConnectionDest = RailGraphDatastore.TryGetRailComponentID(connectionInfo.FromNode, out var fromDest) ? fromDest : null;
                if (fromConnectionDest == null) return null;
                
                // ToNodeの情報を取得
                // Get ToNode information
                var toConnectionDest = RailGraphDatastore.TryGetRailComponentID(connectionInfo.ToNode, out var toDest) ? toDest : null;
                if (toConnectionDest == null) return null;
                
                // RailControlPointを取得
                // Get RailControlPoint
                var fromControlPoint = fromConnectionDest.IsFront ? connectionInfo.FromNode.FrontControlPoint : connectionInfo.FromNode.BackControlPoint;
                var toControlPoint = toConnectionDest.IsFront ? connectionInfo.ToNode.FrontControlPoint : connectionInfo.ToNode.BackControlPoint;
                
                if (fromControlPoint == null || toControlPoint == null) return null;
                
                return new RailConnectionData(
                    new RailNodeInfo(connectionInfo.FromNode, fromConnectionDest),
                    new RailNodeInfo(connectionInfo.ToNode, toConnectionDest),
                    connectionInfo.Distance
                );
            }
        }
        
        [MessagePackObject]
        public class RailNodeInfo
        {
            [Key(0)] public RailComponentIDMessagePack ComponentId { get; set; }
            [Key(1)] public bool IsFrontSide { get; set; }
            [Key(2)] public RailControlPointMessagePack ControlPoint { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailNodeInfo() { }
            
            /// <summary>
            /// RailNodeとConnectionDestinationからRailNodeInfoを生成するコンストラクタ
            /// Constructor to create RailNodeInfo from RailNode and ConnectionDestination
            /// </summary>
            public RailNodeInfo(RailNode node, ConnectionDestination connectionDestination)
            {
                // RailControlPointを取得
                // Get RailControlPoint
                var controlPoint = connectionDestination.IsFront ? node.FrontControlPoint : node.BackControlPoint;
                if (controlPoint == null)
                {
                    throw new ArgumentException("RailControlPointが取得できませんでした");
                }
                
                ComponentId = new RailComponentIDMessagePack(connectionDestination.DestinationID.Position, connectionDestination.DestinationID.ID);
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
}

