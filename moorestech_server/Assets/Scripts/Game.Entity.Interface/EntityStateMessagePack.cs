using System;
using Core.Master;
using MessagePack;
using UnityEngine;

namespace Game.Entity.Interface
{
    // エンティティ状態データのMessagePackコンテナ
    // MessagePack containers representing entity state payloads
    [MessagePackObject]
    public class BeltConveyorItemEntityStateMessagePack
    {
        [Key(0)] public int ItemId { get; set; }
        [Key(1)] public int Count { get; set; }
        [Key(2)] public Guid? SourceConnectorGuid { get; set; }
        [Key(3)] public Guid? GoalConnectorGuid { get; set; }
        [Key(4)] public float RemainingPercent { get; set; }
        [Key(5)] public int BlockPosX { get; set; }
        [Key(6)] public int BlockPosY { get; set; }
        [Key(7)] public int BlockPosZ { get; set; }

        public BeltConveyorItemEntityStateMessagePack() { }

        public BeltConveyorItemEntityStateMessagePack(ItemId itemId, int count, Guid? sourceConnectorGuid, Guid? goalConnectorGuid, float remainingPercent, Vector3Int blockPosition)
        {
            ItemId = itemId.AsPrimitive();
            Count = count;
            SourceConnectorGuid = sourceConnectorGuid;
            GoalConnectorGuid = goalConnectorGuid;
            RemainingPercent = remainingPercent;
            BlockPosX = blockPosition.x;
            BlockPosY = blockPosition.y;
            BlockPosZ = blockPosition.z;
        }
    }
}
