using Server.Event;
using Server.PacketHandle;

namespace Server
{
    public static class StartServer
    {
        public static void Main(string[] args)
        {
            Init();
            PacketHandler.StartServer();
        }

        static void Init()
        {
            RegisterSendClientEvents.Instance.Init();
        }
    }
}