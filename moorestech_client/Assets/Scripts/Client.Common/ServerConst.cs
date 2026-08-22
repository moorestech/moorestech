using static System.IO.Path;

namespace Client.Common
{
    public class ServerConst
    {
        public const string LocalServerIp = "127.0.0.1";

        public static string CreateServerModsDirectory(string serverDirectory)
        {
            return Combine(serverDirectory, "mods");
        }
    }
}