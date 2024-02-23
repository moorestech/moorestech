using System;
using System.Linq;
using Core.Item;
using Game.PlayerInventory.Interface.Event;
using MessagePack;
using Server.Util.MessagePack;

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
            var payload = MessagePackSerializer.Serialize(new GrabInventoryUpdateEventMessagePack(
                playerInventoryUpdateEvent.ItemStack
            )).ToList();

            _eventProtocolProvider.AddEvent(playerInventoryUpdateEvent.PlayerId, payload);
        }
    }


    [MessagePackObject(true)]
    public class GrabInventoryUpdateEventMessagePack : EventProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public GrabInventoryUpdateEventMessagePack()
        {
        }

        public GrabInventoryUpdateEventMessagePack(IItemStack item)
        {
            EventTag = GrabInventoryUpdateEventPacket.EventTag;
            Item = new ItemMessagePack(item);
        }

        public ItemMessagePack Item { get; set; }
    }
}