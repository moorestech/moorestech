using static System.IO.Path;

namespace Client.Common
{
    public class ServerConst
    {
        public const string LocalServerIp = "127.0.0.1";
        public const int LocalServerPort = 11564;

        public const int DefaultPlayerId = 1;


        public static readonly string ServerDirName = "Server";
        public static readonly string ServerDirectory = GetFullPath("./" + ServerDirName);

        public static readonly string ServerExePath = Combine(ServerDirectory, "moorestech_server.exe");
        public static readonly string ServerModsDirectory = Combine(ServerDirectory, "mods");
    }
}