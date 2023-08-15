using System.IO;
using static System.IO.Path;

namespace GameConst
{
    public class ServerConst
    {
        public const string LocalServerIp = "127.0.0.1";
        public const int LocalServerPort = 11564;

        public const int DefaultPlayerId = 1;
        
        
        public static readonly string ServerDirName = "Server";
        public static readonly string ServerDirectory = GetFullPath("./" + ServerDirName);
        
        public static readonly string ServerDllPath = Combine(ServerDirectory,"moorestech_server.dll");
        public static readonly string ServerModsDirectory = Combine(ServerDirectory,"mods");
        
        
        public static readonly string DotnetRuntimeDir = Combine(ServerDirectory,"dotnet-runtime");
#if UNITY_EDITOR_WIN
        public static readonly string DotnetRuntimePath = Combine(DotnetRuntimeDir,"win-x64","dotnet.exe");
#elif UNITY_EDITOR_OSX
        public static readonly string DotnetRuntimePath = Combine(DotnetRuntimeDir,"osx-x64","dotnet");
#elif UNITY_STANDALONE_WIN
        public static readonly string DotnetRuntimePath = Combine(DotnetRuntimeDir,"win-x64","dotnet.exe");
#elif UNITY_STANDALONE_LINUX
        public static readonly string DotnetRuntimePath = Combine(DotnetRuntimeDir,"linux-x64","dotnet");
#elif UNITY_STANDALONE // win、linux以外の場合はmac osと判断する
        public static readonly string DotnetRuntimePath = Combine(DotnetRuntimeDir,"osx-x64","dotnet.exe");
#endif
    }
}