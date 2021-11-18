using Server.Event;
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
            new RegisterSendClientEvents(blockPlace);
            new PacketHandler().StartServer(new PacketResponseCreator(new WorldBlockDatastore(blockPlace)));
        }
    }
}