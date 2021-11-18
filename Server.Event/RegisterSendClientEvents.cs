using World.Event;

namespace Server.Event
{
    public class RegisterSendClientEvents
    {
        public RegisterSendClientEvents(BlockPlaceEvent blockPlaceEvent)
        {
            new ReceivePlaceBlockEvent(blockPlaceEvent);
        }
    }
}