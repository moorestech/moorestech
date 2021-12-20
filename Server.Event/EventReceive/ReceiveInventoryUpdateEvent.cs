using System.Collections.Generic;
using Game.PlayerInventory.Interface.Event;
using Server.Util;

namespace Server.Event.EventReceive
{
    public class ReceiveInventoryUpdateEvent
    {
        private readonly EventProtocolProvider _eventProtocolProvider;
        private const short EventId = 1;

        public ReceiveInventoryUpdateEvent(IPlayerInventoryUpdateEvent inventoryUpdateEvent,EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            inventoryUpdateEvent.Subscribe(ReceivedEvent);
        }
        
        
        private void ReceivedEvent(PlayerInventoryUpdateEventProperties playerInventoryUpdateEvent)
        {
            var payload = new List<byte>();
            
            
            payload.AddRange(ByteListConverter.ToByteArray(ServerEventConst.EventPacketId));
            payload.AddRange(ByteListConverter.ToByteArray(EventId));
            payload.AddRange(ByteListConverter.ToByteArray(playerInventoryUpdateEvent.InventorySlot));
            payload.AddRange(ByteListConverter.ToByteArray(playerInventoryUpdateEvent.ItemId));
            payload.AddRange(ByteListConverter.ToByteArray(playerInventoryUpdateEvent.Count));
            
            _eventProtocolProvider.AddEvent(playerInventoryUpdateEvent.PlayerId,payload.ToArray());

        }
    }
}