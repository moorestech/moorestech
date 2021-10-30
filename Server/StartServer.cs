using Server.Event;
using Server.PacketHandle;
using World;

namespace Server
{
    public static class StartServer
    {
        public static void Main(string[] args)
        {
            Init();
            new PacketHandler().StartServer(new PacketResponseCreator(new WorldBlockDatastore()));
        }

        static void Init()
        {
            RegisterSendClientEvents.Instance.Init();
        }
    }
}