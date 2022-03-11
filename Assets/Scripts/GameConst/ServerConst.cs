using static System.IO.Path;

namespace GameConst
{
    public class ServerConst
    {
        public const string LocalServerIp = "127.0.0.1";
        public const int LocalServerPort = 11564;

        public const int DefaultPlayerId = 1;
        
        
#if UNITY_EDITOR_WIN
        public static readonly string ServerExePath = GetFullPath("./WindowsServer/moorestech_server.exe");
        public static readonly string ServerConfigPath = GetFullPath("./WindowsServer/Config");
#elif UNITY_STANDALONE_WIN
        public static readonly string ServerExePath = GetFullPath("./server/moorestech_server.exe");
        public static readonly string ServerConfigPath = GetFullPath("./server/Config");
#endif
    }
}