using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Game.Paths
{
    public class GameSystemPath
    {
        public static string GameFileRootDirectory
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return DirectoryCreator("C:\\Users", Environment.UserName, "AppData", "Roaming", ".moorestech");
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return DirectoryCreator("/Users", Environment.UserName, "Library", "Application Support", "moorestech");
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return DirectoryCreator("/home", Environment.UserName, ".moorestech");
                }
                throw new Exception("Unsupported OS");
            }
        }

        public static string TmpFileDirectory => DirectoryCreator(GameFileRootDirectory, "tmp");                                                                                                                                                             
        public static string ExtractedModDirectory => DirectoryCreator(TmpFileDirectory, "extracted_mods");
        public static string SaveFileDirectory => DirectoryCreator(GameFileRootDirectory, "saves");
        
        public static string GetExtractedModDirectory(string folderName) => DirectoryCreator(ExtractedModDirectory, folderName);
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