using System;
using System.IO;

namespace Game.Paths
{
    public class SystemPath
    {
        public static string GameFileRootDirectory
        {
            get
            {
                var path = "";
                switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.Win32NT:
                    case PlatformID.Win32S:
                    case PlatformID.Win32Windows:
                    case PlatformID.WinCE:
                        path = Path.Combine("C:", "Users", Environment.UserName, "AppData", "Roaming", ".moorestech");
                        break;
                    case PlatformID.Unix:
                        path = Path.Combine("/Users", Environment.UserName, "Library", "Application Support", "moorestech");
                        break;
                    default:
                        throw new Exception("Unsupported OS");
                        break;
                }
                return path;
            }
        }



        public static string GetSaveFilePath(string fileName)
        {
            return Path.Combine(GameFileRootDirectory,"saves", fileName);
        }
    }
}