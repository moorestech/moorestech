using PlayerInventory.Event;

namespace Server.Event.EventReceive
{
    public class ReceiveInventoryUpdateEvent
    {
        private readonly EventProtocolProvider _eventProtocolProvider;

        public ReceiveInventoryUpdateEvent(PlayerInventoryUpdateEvent playerInventoryUpdateEvent,EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            playerInventoryUpdateEvent.OnPlayerInventoryUpdate += ReceivedEvent;
        }
        
        
        private void ReceivedEvent(PlayerInventoryUpdateEventProperties blockPlaceEventProperties)
        {
            //TODO プロトコルの実装

        }
    }
}