using System;
using Core.Item.Interface;
using Game.PlayerInventory.Interface.Event;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Event.EventReceive
{
    public class MainInventoryUpdateEventPacket
    {
        public const string EventTag = "va:event:mainInvUpdate";
        private readonly EventProtocolProvider _eventProtocolProvider;
        
        public MainInventoryUpdateEventPacket(IMainInventoryUpdateEvent mainInventoryUpdateEvent,
            EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            mainInventoryUpdateEvent.Subscribe(ReceivedEvent);
        }
        
        
        private void ReceivedEvent(PlayerInventoryUpdateEventProperties playerInventoryUpdateEvent)
        {
            var messagePack = new MainInventoryUpdateEventMessagePack(playerInventoryUpdateEvent.InventorySlot, playerInventoryUpdateEvent.ItemStack);
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            _eventProtocolProvider.AddEvent(playerInventoryUpdateEvent.PlayerId, EventTag, payload);
        }
    }
    
    
    [MessagePackObject]
    public class MainInventoryUpdateEventMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public MainInventoryUpdateEventMessagePack()
        {
        }
        
        public MainInventoryUpdateEventMessagePack(int slot, IItemStack itemStack)
        {
            Slot = slot;
            Item = new ItemMessagePack(itemStack.Id, itemStack.Count);
        }
        
        [Key(0)] public int Slot { get; set; }
        
        [Key(1)] public ItemMessagePack Item { get; set; }
    }
}