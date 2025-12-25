using System;
using Core.Master;
using MessagePack;

namespace Game.Entity.Interface
{
    // エンティティ状態データのMessagePackコンテナ
    // MessagePack containers representing entity state payloads
    [MessagePackObject]
    public class BeltConveyorItemEntityStateMessagePack
    {
        [Key(0)] public int ItemId { get; set; }
        [Key(1)] public int Count { get; set; }
        [Key(2)] public string SourceConnectorGuid { get; set; }
        [Key(3)] public string GoalConnectorGuid { get; set; }

        public BeltConveyorItemEntityStateMessagePack() { }

        public BeltConveyorItemEntityStateMessagePack(ItemId itemId, int count, string sourceConnectorGuid, string goalConnectorGuid)
        {
            ItemId = itemId.AsPrimitive();
            Count = count;
            SourceConnectorGuid = sourceConnectorGuid;
            GoalConnectorGuid = goalConnectorGuid;
        }
    }
    
    [MessagePackObject]
    public class TrainEntityStateMessagePack
    {
        [Key(0)] public Guid TrainCarId { get; set; }
        [Key(1)] public Guid TrainMasterId { get; set; }
        
        public TrainEntityStateMessagePack() { }
        
        public TrainEntityStateMessagePack(Guid trainCarId, Guid trainMasterId)
        {
            TrainCarId = trainCarId;
            TrainMasterId = trainMasterId;
        }
    }
}
