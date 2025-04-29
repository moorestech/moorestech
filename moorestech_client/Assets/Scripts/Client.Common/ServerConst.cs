using static System.IO.Path;

namespace Client.Common
{
    public class ServerConst
    {
        public const string LocalServerIp = "127.0.0.1";
        public const int LocalServerPort = 11564;
        
        public const int DefaultPlayerId = 1;
        
        
        public static string CreateServerModsDirectory(string serverDirectory)
        {
            return Combine(serverDirectory, "mods");
        }
    }
}