using System;
using System.Runtime.InteropServices;
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

        public static readonly string ServerDllPath = Combine(ServerDirectory, "moorestech_server.dll");
        public static readonly string ServerModsDirectory = Combine(ServerDirectory, "mods");


        public static readonly string DotnetRuntimeDir = Combine(ServerDirectory, "dotnet-runtime");
        public static readonly string DotnetRuntimeBinaryName = "dotnet";


        public static string DotnetRuntimePath
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                    RuntimeInformation.ProcessArchitecture == Architecture.X64)
                    return Combine(DotnetRuntimeDir, "win-x64", DotnetRuntimeBinaryName + ".exe");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
                    RuntimeInformation.ProcessArchitecture == Architecture.X64)
                    return Combine(DotnetRuntimeDir, "osx-x64", DotnetRuntimeBinaryName);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
                    RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                    return Combine(DotnetRuntimeDir, "osx-arm64", DotnetRuntimeBinaryName);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
                    RuntimeInformation.ProcessArchitecture == Architecture.X64)
                    return Combine(DotnetRuntimeDir, "linux-x64", DotnetRuntimeBinaryName);

                throw new Exception("Unsupported OS");
            }
        }
    }
}