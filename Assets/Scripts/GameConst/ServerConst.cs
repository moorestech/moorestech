using System.IO;
using static System.IO.Path;

namespace GameConst
{
    public class ServerConst
    {
        public const string LocalServerIp = "127.0.0.1";
        public const int LocalServerPort = 11564;

        public const int DefaultPlayerId = 1;
        
        public const string StandAloneServerDirectory = "server";
        
#if UNITY_EDITOR_WIN
        public static readonly string ServerDirectory = GetFullPath("./WindowsServer");
#elif UNITY_EDITOR_OSX
            //TODO:
        public static readonly string ServerDirectory = GetFullPath("./WindowsServer");
#elif UNITY_STANDALONE_WIN
        public static readonly string ServerDirectory = GetFullPath("./" + StandAloneServerDirectory);
#endif
        public static readonly string ServerExePath = Combine(ServerDirectory,"moorestech_server.exe");
        public static readonly string ServerConfigDirectory = Combine(ServerDirectory,"Config");
    }
}