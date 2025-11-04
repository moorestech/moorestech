using System;
using MessagePack;

namespace Game.Entity.Interface
{
    // エンティティ状態データのMessagePackコンテナ
    // MessagePack containers representing entity state payloads
    [MessagePackObject]
    public class ItemEntityStateMessagePack
    {
        public ItemEntityStateMessagePack()
        {
        }
        
        public ItemEntityStateMessagePack(int itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
        
        [Key(0)] public int ItemId { get; set; }
        [Key(1)] public int Count { get; set; }
    }
    
    [MessagePackObject]
    public class TrainEntityStateMessagePack
    {
        public TrainEntityStateMessagePack()
        {
        }
        
        public TrainEntityStateMessagePack(Guid trainId)
        {
            TrainId = trainId;
        }
        
        [Key(0)] public Guid TrainId { get; set; }
    }
}
