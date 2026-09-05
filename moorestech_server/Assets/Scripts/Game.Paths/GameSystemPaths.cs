using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Game.Paths
{
    public static class GameSystemPaths
    {
        // ワールドID桁数の正本。生成側(WorldIdentity)と検証側が共有する
        // Single source of truth for the world id length, shared by the generator (WorldIdentity) and this validator
        public const int WorldIdHexDigits = 16;

        // 中断時の残骸と正規のワールドを区別するための一時サフィックス
        // Temp suffix that tells an interrupted remnant apart from the canonical world
        private const string IncompleteSuffix = ".incomplete";

        // 起動時の既定ワールド名。正はここ1箇所
        // The default world name at boot; single source of truth
        public const string DefaultWorldName = "world_1";

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
        public static string DefaultWorldDirectory => GetSaveFilePath(DefaultWorldName);

        // サーバーから受け取った派生データの置き場。削除しても再取得で復元される
        // Holds data derived from the server; deleting it only forces a re-fetch
        public static string WorldCacheDirectory => DirectoryCreator(GameSystemDirectory, "cache", "worlds");

        // ワールドごとのクライアントキャッシュ。worldIdはサーバーが払い出すワールド同一性の識別子
        // Per-world client cache; worldId is the world identity issued by the server
        public static string GetWorldCacheDirectory(string worldId)
        {
            // wire由来IDは生成規則どおりのlower-hexだけを許し、キャッシュ外へのパス逸脱を入口で拒否する
            // Only the server's lower-hex ID format is accepted from the wire, rejecting cache-path escape at the boundary
            if (!IsValidWorldId(worldId))
                throw new ArgumentException(
                    $"World id must be exactly {WorldIdHexDigits} lowercase hexadecimal characters.", nameof(worldId));

            var cacheRoot = Path.GetFullPath(WorldCacheDirectory);
            var worldDirectory = Path.GetFullPath(Path.Combine(cacheRoot, worldId));
            var cacheRootPrefix = cacheRoot + Path.DirectorySeparatorChar;
            if (!worldDirectory.StartsWith(cacheRootPrefix, StringComparison.Ordinal))
                throw new ArgumentException("World id resolved outside the world cache directory.", nameof(worldId));

            return DirectoryCreator(worldDirectory);

            #region Internal

            bool IsValidWorldId(string value)
            {
                if (value == null || value.Length != WorldIdHexDigits) return false;
                foreach (var character in value)
                    if (!('0' <= character && character <= '9') && !('a' <= character && character <= 'f'))
                        return false;
                return true;
            }

            #endregion
        }

        // 既定ワールドを丸ごと消す。パスの正本と削除手順を同居させ、リセット経路ごとの二重実装を防ぐ
        // Wipe the default world here so deletion lives with the canonical path, never duplicated per reset path
        public static bool DeleteDefaultWorldDirectory()
        {
            var worldDirectory = DefaultWorldDirectory;
            if (!Directory.Exists(worldDirectory)) return false;

            // 退避してから消す。途中失敗しても正規名が半端に壊れた状態では残らない
            // Move aside before deleting so a mid-failure never leaves the canonical name half-broken
            var pendingDeletion = worldDirectory + IncompleteSuffix;
            DiscardRemnant(pendingDeletion);
            Directory.Move(worldDirectory, pendingDeletion);
            Directory.Delete(pendingDeletion, true);
            return true;
        }

        // 前回の中断で残った一時ディレクトリを捨てて再入を通す
        // Drops a temp directory left by an earlier interruption so the retry can proceed
        private static void DiscardRemnant(string incompletePath)
        {
            if (Directory.Exists(incompletePath)) Directory.Delete(incompletePath, true);
        }

        public static string GetExtractedModDirectory(string folderName)
        {
            return Path.Combine(ExtractedModDirectory, folderName);
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
