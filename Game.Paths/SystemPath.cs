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
                switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.Win32NT:
                    case PlatformID.Win32S:
                    case PlatformID.Win32Windows:
                    case PlatformID.WinCE:
                        return DirectoryCreator("C:", "Users", Environment.UserName, "AppData", "Roaming", ".moorestech");
                    case PlatformID.Unix:
                    case PlatformID.MacOSX:
                        return DirectoryCreator("/Users", Environment.UserName, "Library", "Application Support", "moorestech");
                    case PlatformID.Xbox:
                    case PlatformID.Other:
                    default:
                        throw new Exception("Unsupported OS");
                }
            }
        }

        public static string TmpFileDirectory => DirectoryCreator(GameFileRootDirectory, "tmp");                                                                                                                                                             
        public static string ExtractedModDirectory => DirectoryCreator(TmpFileDirectory, "extracted_mods");
        public static string SaveFileDirectory => DirectoryCreator(GameFileRootDirectory, "saves");
        
        
        public static string GetSaveFilePath(string fileName) => Path.Combine(SaveFileDirectory, fileName);
        
        
        private static string DirectoryCreator(params string[] paths)
        {
            var directory = Path.Combine(paths);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            return directory;
        }
    }
}