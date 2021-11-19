using World.Event;

namespace Server.Event
{
    public class RegisterSendClientEvents
    {
        public RegisterSendClientEvents(BlockPlaceEvent blockPlaceEvent,EventProtocolProvider eventProtocolProvider)
        {
            new ReceivePlaceBlockEvent(blockPlaceEvent,eventProtocolProvider);
        }
    }
}