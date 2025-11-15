using System;
using Core.Item.Interface;
using Core.Master;
using MessagePack;

namespace Server.Util.MessagePack
{
    [MessagePackObject]
    public class ItemMessagePack
    {
        [Key(0)] public ItemId Id { get; set; }
        
        [Key(1)] public int Count { get; set; }
        
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ItemMessagePack()
        {
        }
        
        public ItemMessagePack(ItemId id, int count)
        {
            Id = id;
            Count = count;
        }
        
        public ItemMessagePack(IItemStack itemStack)
        {
            Id = itemStack.Id;
            Count = itemStack.Count;
        }
    }
}