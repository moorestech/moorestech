using World.Event;

namespace Server.Event
{
    public class RegisterSendClientEvents
    {
        public RegisterSendClientEvents(BlockPlaceEvent blockPlaceEvent,EventProtocolQueProvider eventProtocolQueProvider)
        {
            new ReceivePlaceBlockEvent(blockPlaceEvent,eventProtocolQueProvider);
        }
    }
}