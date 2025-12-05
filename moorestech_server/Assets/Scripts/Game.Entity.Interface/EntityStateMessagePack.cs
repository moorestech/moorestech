using System;
using System.Collections.Generic;
using Core.Master;
using MessagePack;
using UnityEngine;

namespace Game.Entity.Interface
{
    // エンティティ状態データのMessagePackコンテナ
    // MessagePack containers representing entity state payloads
    [MessagePackObject]
    public class ItemEntityStateMessagePack
    {
        [Key(0)] public int ItemId { get; set; }
        [Key(1)] public int Count { get; set; }

        public ItemEntityStateMessagePack() { }

        public ItemEntityStateMessagePack(ItemId itemId, int count)
        {
            ItemId = itemId.AsPrimitive();
            Count = count;
        }
    }

    [MessagePackObject]
    public class TrainEntityStateMessagePack
    {
        [Key(0)] public Guid TrainCarId { get; set; }
        [Key(1)] public Guid TrainMasterId { get; set; }
        [Key(2)] public RailPositionMessagePack RailPosition { get; set; }

        public TrainEntityStateMessagePack() { }

        public TrainEntityStateMessagePack(Guid trainCarId, Guid trainMasterId, RailPositionMessagePack railPosition)
        {
            TrainCarId = trainCarId;
            TrainMasterId = trainMasterId;
            RailPosition = railPosition;
        }
    }

    /// <summary>
    /// RailPositionをシリアライズするためのMessagePackクラス
    /// MessagePack class for serializing RailPosition
    /// </summary>
    [MessagePackObject]
    public class RailPositionMessagePack
    {
        /// <summary>
        /// RailNodeのリスト（インデックスが小さいほうに向かって進む）
        /// List of RailNodes (advancing toward smaller indices)
        /// </summary>
        [Key(0)] public List<RailNodeDataMessagePack> RailNodes { get; set; }

        /// <summary>
        /// 先頭の前輪が次のノードまでどれだけ離れているか
        /// Distance from front wheel to next node
        /// </summary>
        [Key(1)] public int DistanceToNextNode { get; set; }

        /// <summary>
        /// 列車の長さ
        /// Train length
        /// </summary>
        [Key(2)] public int TrainLength { get; set; }

        public RailPositionMessagePack() { }

        public RailPositionMessagePack(List<RailNodeDataMessagePack> railNodes, int distanceToNextNode, int trainLength)
        {
            RailNodes = railNodes;
            DistanceToNextNode = distanceToNextNode;
            TrainLength = trainLength;
        }
    }

    /// <summary>
    /// RailNodeの位置情報を保持するMessagePackクラス（循環参照回避用）
    /// MessagePack class for RailNode position data (to avoid circular dependency)
    /// </summary>
    ///TODO: Server.UtilがGame.Entity.Interfaceへ依存をしているため、こちら側からVector3MessagePack等が使えず、XYZ成分をそれぞれ保持している
    [MessagePackObject]
    public class RailNodeDataMessagePack
    {
        /// <summary>
        /// RailComponentのブロック座標X
        /// Block position X of RailComponent
        /// </summary>
        [Key(0)] public int ComponentPositionX { get; set; }

        /// <summary>
        /// RailComponentのブロック座標Y
        /// Block position Y of RailComponent
        /// </summary>
        [Key(1)] public int ComponentPositionY { get; set; }

        /// <summary>
        /// RailComponentのブロック座標Z
        /// Block position Z of RailComponent
        /// </summary>
        [Key(2)] public int ComponentPositionZ { get; set; }
        
        [IgnoreMember]
        public Vector3Int ComponentPosition => new Vector3Int(ComponentPositionX, ComponentPositionY, ComponentPositionZ);

        /// <summary>
        /// RailComponentのID
        /// ID of RailComponent
        /// </summary>
        [Key(3)] public int ComponentId { get; set; }

        /// <summary>
        /// FrontSide（true）かBackSide（false）か
        /// Whether it's front side (true) or back side (false)
        /// </summary>
        [Key(4)] public bool IsFrontSide { get; set; }

        /// <summary>
        /// 制御点の元の位置X
        /// Original position X of control point
        /// </summary>
        [Key(5)] public float OriginalPositionX { get; set; }

        /// <summary>
        /// 制御点の元の位置Y
        /// Original position Y of control point
        /// </summary>
        [Key(6)] public float OriginalPositionY { get; set; }

        /// <summary>
        /// 制御点の元の位置Z
        /// Original position Z of control point
        /// </summary>
        [Key(7)] public float OriginalPositionZ { get; set; }
        
        [IgnoreMember]
        public Vector3 OriginalPosition => new Vector3(OriginalPositionX, OriginalPositionY, OriginalPositionZ);

        /// <summary>
        /// 制御点の位置X
        /// Control point position X
        /// </summary>
        [Key(8)] public float ControlPointPositionX { get; set; }

        /// <summary>
        /// 制御点の位置Y
        /// Control point position Y
        /// </summary>
        [Key(9)] public float ControlPointPositionY { get; set; }

        /// <summary>
        /// 制御点の位置Z
        /// Control point position Z
        /// </summary>
        [Key(10)] public float ControlPointPositionZ { get; set; }
        
        [IgnoreMember]
        public Vector3 ControlPointPosition => new Vector3(ControlPointPositionX, ControlPointPositionY, ControlPointPositionZ);

        public RailNodeDataMessagePack() { }

        public RailNodeDataMessagePack(Vector3Int componentPosition, int componentId, bool isFrontSide, Vector3 originalPosition, Vector3 controlPointPosition)
        {
            ComponentPositionX = componentPosition.x;
            ComponentPositionY = componentPosition.y;
            ComponentPositionZ = componentPosition.z;
            ComponentId = componentId;
            IsFrontSide = isFrontSide;
            OriginalPositionX = originalPosition.x;
            OriginalPositionY = originalPosition.y;
            OriginalPositionZ = originalPosition.z;
            ControlPointPositionX = controlPointPosition.x;
            ControlPointPositionY = controlPointPosition.y;
            ControlPointPositionZ = controlPointPosition.z;
        }

        /// <summary>
        /// Vector3Intとしてコンポーネント位置を取得
        /// Get component position as Vector3Int
        /// </summary>
        public Vector3Int GetComponentPosition() => new(ComponentPositionX, ComponentPositionY, ComponentPositionZ);

        /// <summary>
        /// Vector3として元の位置を取得
        /// Get original position as Vector3
        /// </summary>
        public Vector3 GetOriginalPosition() => new(OriginalPositionX, OriginalPositionY, OriginalPositionZ);

        /// <summary>
        /// Vector3として制御点位置を取得
        /// Get control point position as Vector3
        /// </summary>
        public Vector3 GetControlPointPosition() => new(ControlPointPositionX, ControlPointPositionY, ControlPointPositionZ);
    }
}
