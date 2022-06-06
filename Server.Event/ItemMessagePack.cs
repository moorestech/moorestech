using System;
using Core.Item;
using MessagePack;

namespace Server.Event
{

    [MessagePackObject(false)]
    public class ItemMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ItemMessagePack() { }

        public ItemMessagePack(int id, int count)
        {
            Id = id;
            Count = count;
        }

        public ItemMessagePack(IItemStack itemStack)
        {
            Id = itemStack.Id;
            Count = itemStack.Count;
        }

        [Key(0)]
        public int Id { get; set; }
        [Key(1)]
        public int Count { get; set; }
        
    }
}