using PlayerInventory;
using Server.Event;
using Server.Event.EventReceive.EventRegister;
using Server.PacketHandle;
using World;
using World.Event;

namespace Server
{
    public static class StartServer
    {
        public static void Main(string[] args)
        {
            var blockPlace = new BlockPlaceEvent();
            var eventProtocol = new EventProtocolProvider();
            new RegisterSendClientEvents(blockPlace,eventProtocol);
            new PacketHandler().StartServer(new PacketResponseCreator(new WorldBlockDatastore(blockPlace),eventProtocol,new PlayerInventoryDataStore()));
        }
    }
}