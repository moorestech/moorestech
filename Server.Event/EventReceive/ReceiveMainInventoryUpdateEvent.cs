using System.Collections.Generic;
using Game.PlayerInventory.Interface.Event;
using Server.Util;

namespace Server.Event.EventReceive
{
    public class ReceiveMainInventoryUpdateEvent
    {
        private readonly EventProtocolProvider _eventProtocolProvider;
        private const short EventId = 1;

        public ReceiveMainInventoryUpdateEvent(IPlayerMainInventoryUpdateEvent mainInventoryUpdateEvent,
            EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            mainInventoryUpdateEvent.Subscribe(ReceivedEvent);
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