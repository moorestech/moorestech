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
        
        public static string TmpFileDirectory => DirectoryCreator(GameSystemDirectory, "Tmp");
        public static string ExtractedModDirectory => DirectoryCreator(TmpFileDirectory, "ExtractedMods");
        public static string SaveFileDirectory => DirectoryCreator(GameSystemDirectory, "Saves");

        // サーバーから受け取った派生データの置き場。削除しても再取得で復元される
        // Holds data derived from the server; deleting it only forces a re-fetch
        public static string WorldCacheDirectory => DirectoryCreator(GameSystemDirectory, "cache", "worlds");

        // ワールドごとのクライアントキャッシュ。worldIdはサーバーが払い出すワールド同一性の識別子
        // Per-world client cache; worldId is the world identity issued by the server
        public static string GetWorldCacheDirectory(string worldId)
        {
            // 空のworldIdはworlds直下を指してしまい、全ワールドのキャッシュを混在させる
            // An empty worldId would point at the worlds root and mix every world's cache together
            if (string.IsNullOrEmpty(worldId)) throw new ArgumentException("World id must not be empty.", nameof(worldId));
            return DirectoryCreator(WorldCacheDirectory, worldId);
        }

        public static string GetExtractedModDirectory(string folderName)
        {
            return Path.Combine(ExtractedModDirectory, folderName);
        }
        
        public static string CreateExtractedModDirectory(string folderName)
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