using System.Collections.Generic;
using Game.PlayerInventory.Interface.Event;
using Server.Util;

namespace Server.Event.EventReceive
{
    public class ReceiveInventoryUpdateEvent
    {
        private readonly EventProtocolProvider _eventProtocolProvider;
        private const short EventId = 1;

        public ReceiveInventoryUpdateEvent(IPlayerInventoryUpdateEvent inventoryUpdateEvent,
            EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            inventoryUpdateEvent.Subscribe(ReceivedEvent);
        }


        private void ReceivedEvent(PlayerInventoryUpdateEventProperties playerInventoryUpdateEvent)
        {
            var payload = new List<byte>();


            payload.AddRange(ToByteList.Convert(ServerEventConst.EventPacketId));
            payload.AddRange(ToByteList.Convert(EventId));
            payload.AddRange(ToByteList.Convert(playerInventoryUpdateEvent.InventorySlot));
            payload.AddRange(ToByteList.Convert(playerInventoryUpdateEvent.ItemId));
            payload.AddRange(ToByteList.Convert(playerInventoryUpdateEvent.Count));

            _eventProtocolProvider.AddEvent(playerInventoryUpdateEvent.PlayerId, payload.ToArray());
        }
    }
}