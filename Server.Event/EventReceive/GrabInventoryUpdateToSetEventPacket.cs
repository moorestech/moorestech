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
    public class GrabInventoryUpdateToSetEventPacket
    {
        private readonly EventProtocolProvider _eventProtocolProvider;
        public const string EventTag = "va:event:grabInvUpdate";

        public GrabInventoryUpdateToSetEventPacket(IGrabInventoryUpdateEvent grabInventoryUpdateEvent,
            EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            grabInventoryUpdateEvent.Subscribe(ReceivedEvent);
        }


        private void ReceivedEvent(PlayerInventoryUpdateEventProperties playerInventoryUpdateEvent)
        {
            _eventProtocolProvider.AddEvent(playerInventoryUpdateEvent.PlayerId, new GrabInventoryUpdateEventMessagePack(
                playerInventoryUpdateEvent.ItemStack));
        }
    }
    
        
    [MessagePackObject(keyAsPropertyName :true)]
    public class GrabInventoryUpdateEventMessagePack : EventProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public GrabInventoryUpdateEventMessagePack() { }

        public GrabInventoryUpdateEventMessagePack(IItemStack item)
        {
            EventTag = GrabInventoryUpdateToSetEventPacket.EventTag;
            Item = new ItemMessagePack(item);
        }

        public ItemMessagePack Item { get; set; }
    }
}