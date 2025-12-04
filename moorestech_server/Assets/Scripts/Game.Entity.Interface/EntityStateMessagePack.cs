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
    [MessagePackObject]
    public class RailNodeDataMessagePack
    {
        /// <summary>
        /// RailComponentのブロック座標
        /// Block position of RailComponent
        /// </summary>
        [Key(0)] public Vector3Int ComponentPosition { get; set; }

        /// <summary>
        /// RailComponentのID
        /// ID of RailComponent
        /// </summary>
        [Key(1)] public int ComponentId { get; set; }

        /// <summary>
        /// FrontSide（true）かBackSide（false）か
        /// Whether it's front side (true) or back side (false)
        /// </summary>
        [Key(2)] public bool IsFrontSide { get; set; }

        /// <summary>
        /// 制御点の元の位置
        /// Original position of control point
        /// </summary>
        [Key(3)] public Vector3 OriginalPosition { get; set; }

        /// <summary>
        /// 制御点の位置
        /// Control point position
        /// </summary>
        [Key(4)] public Vector3 ControlPointPosition { get; set; }

        public RailNodeDataMessagePack() { }

        public RailNodeDataMessagePack(Vector3Int componentPosition, int componentId, bool isFrontSide, Vector3 originalPosition, Vector3 controlPointPosition)
        {
            ComponentPosition = componentPosition;
            ComponentId = componentId;
            IsFrontSide = isFrontSide;
            OriginalPosition = originalPosition;
            ControlPointPosition = controlPointPosition;
        }
    }
}
