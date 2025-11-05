using System;
using Core.Master;
using MessagePack;

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
        
        public TrainEntityStateMessagePack() { }
        
        public TrainEntityStateMessagePack(Guid trainCarId)
        {
            TrainCarId = trainCarId;
        }
    }
}
