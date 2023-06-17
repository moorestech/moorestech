using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Game.PlayerInventory.Interface.Event;
using MessagePack;
using Server.Util;
using Server.Util.MessagePack;

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
            
            _eventProtocolProvider.AddEvent(playerInventoryUpdateEvent.PlayerId, new MainInventoryUpdateEventMessagePack(
                playerInventoryUpdateEvent.InventorySlot,playerInventoryUpdateEvent.ItemStack));
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