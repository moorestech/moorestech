using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Game.Paths
{
    public static class GameSystemPaths
    {
        public static string GameSystemDirectory
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return DirectoryCreator("C:\\Users", Environment.UserName, "AppData", "Roaming", ".moorestech");
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return DirectoryCreator("/Users", Environment.UserName, "Library", "Application Support",
                        "moorestech");
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return DirectoryCreator("/home", Environment.UserName, ".moorestech");
                throw new Exception("Unsupported OS");
            }
        }

        public static string TmpFileDirectory => DirectoryCreator(GameSystemDirectory, "tmp");
        public static string ExtractedModDirectory => DirectoryCreator(TmpFileDirectory, "extracted_mods");
        public static string SaveFileDirectory => DirectoryCreator(GameSystemDirectory, "saves");

        public static string GetExtractedModDirectory(string folderName)
        {
            return DirectoryCreator(ExtractedModDirectory, folderName);
        }

        public static string GetSaveFilePath(string fileName)
        {
            return Path.Combine(SaveFileDirectory, fileName);
        }


        private static string DirectoryCreator(params string[] paths)
        {
            var directory = Path.Combine(paths);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            return directory;
        }
    }
}