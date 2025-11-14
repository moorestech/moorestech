using System;
using Core.Item.Interface;
using Game.PlayerInventory.Interface.Event;
using MessagePack;
using Game.Common.MessagePack;

namespace Server.Event.EventReceive
{
    public class GrabInventoryUpdateEventPacket
    {
        public const string EventTag = "va:event:grabInvUpdate";
        private readonly EventProtocolProvider _eventProtocolProvider;
        
        public GrabInventoryUpdateEventPacket(IGrabInventoryUpdateEvent grabInventoryUpdateEvent,
            EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            grabInventoryUpdateEvent.Subscribe(ReceivedEvent);
        }
        
        
        private void ReceivedEvent(PlayerInventoryUpdateEventProperties playerInventoryUpdateEvent)
        {
            var messagePack = new GrabInventoryUpdateEventMessagePack(playerInventoryUpdateEvent.ItemStack);
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            _eventProtocolProvider.AddEvent(playerInventoryUpdateEvent.PlayerId, EventTag, payload);
        }
    }
    
    
    [MessagePackObject]
    public class GrabInventoryUpdateEventMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public GrabInventoryUpdateEventMessagePack()
        {
        }
        
        public GrabInventoryUpdateEventMessagePack(IItemStack item)
        {
            Item = new ItemMessagePack(item);
        }
        
        [Key(0)] public ItemMessagePack Item { get; set; }
    }
}
