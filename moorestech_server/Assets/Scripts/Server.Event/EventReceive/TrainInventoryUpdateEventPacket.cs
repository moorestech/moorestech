using System;
using Core.Item.Interface;
using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Event.EventReceive
{
    public class TrainInventoryUpdateEventPacket
    {
        public const string EventTag = "va:event:trainInvUpdate";
        
        //TODO 足りない部分を実装する
        
        
        [MessagePackObject]
        public class TrainInventoryUpdateEventMessagePack
        {
            [Key(0)] public int EntityInstanceId { get; set; }
            [Key(1)] public int Slot { get; set; }
            [Key(2)] public ItemMessagePack Item { get; set; }
            
            
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public TrainInventoryUpdateEventMessagePack() { }
            
            public TrainInventoryUpdateEventMessagePack(int entityInstanceId, int slot, IItemStack item)
            {
                EntityInstanceId = entityInstanceId;
                Slot = slot;
                Item = new ItemMessagePack(item.Id, item.Count);
            }
        }
    }
}