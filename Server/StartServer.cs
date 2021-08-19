using industrialization.Server.PacketHandle;

namespace industrialization.Server
{
    public static class StartServer
    {
        //Unityとの同期テスト
        public static void Main(string[] args)
        {
            PacketHandler.StartServer();
        }
    }
}