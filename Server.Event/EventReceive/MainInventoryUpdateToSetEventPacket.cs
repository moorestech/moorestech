using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Game.PlayerInventory.Interface.Event;
using MessagePack;
using Server.Util;

namespace Server.Event.EventReceive
{
    public class MainInventoryUpdateToSetEventPacket
    {
        private readonly EventProtocolProvider _eventProtocolProvider;
        public const string EventTag = "va:event:mainInvUpdate";

        public MainInventoryUpdateToSetEventPacket(IMainInventoryUpdateEvent mainInventoryUpdateEvent,
            EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            mainInventoryUpdateEvent.Subscribe(ReceivedEvent);
        }


        private void ReceivedEvent(PlayerInventoryUpdateEventProperties playerInventoryUpdateEvent)
        {
            var payload = MessagePackSerializer.Serialize(new MainInventoryUpdateEventMessagePack(
                playerInventoryUpdateEvent.InventorySlot,playerInventoryUpdateEvent.ItemStack
            )).ToList();
            
            _eventProtocolProvider.AddEvent(playerInventoryUpdateEvent.PlayerId, payload);
        }
    }
    
        
    [MessagePackObject(keyAsPropertyName :true)]
    public class MainInventoryUpdateEventMessagePack : EventProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public MainInventoryUpdateEventMessagePack() { }

        public MainInventoryUpdateEventMessagePack(int slot,IItemStack itemStack)
        {
            EventTag = MainInventoryUpdateToSetEventPacket.EventTag;
            Slot = slot;
            Item = new ItemMessagePack(itemStack.Id, itemStack.Count);
        }

        public int Slot { get; set; }
        public ItemMessagePack Item { get; set; }
    }
}