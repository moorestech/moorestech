using System.IO;

namespace Game.Paths
{
    /// <summary>ワールドディレクトリ内の全ファイル配置を一元定義する値オブジェクト。パス連結はここ以外で行わない</summary>
    /// <summary>Value object owning the entire world-directory layout; no path joins elsewhere</summary>
    public class WorldDataDirectory
    {
        public string Root { get; }
        public string WorldMetaFilePath { get; }
        public string MapJsonFilePath { get; }
        public string SaveJsonFilePath { get; }
        public string TerrainDirectory { get; }
        public string CacheDirectory { get; }
        public string CacheReadmeFilePath { get; }
        public string ProvisioningTempDirectory { get; }

        private WorldDataDirectory(string root, string worldMetaFilePath, string mapJsonFilePath, string saveJsonFilePath,
            string terrainDirectory, string cacheDirectory, string cacheReadmeFilePath, string provisioningTempDirectory)
        {
            Root = root;
            WorldMetaFilePath = worldMetaFilePath;
            MapJsonFilePath = mapJsonFilePath;
            SaveJsonFilePath = saveJsonFilePath;
            TerrainDirectory = terrainDirectory;
            CacheDirectory = cacheDirectory;
            CacheReadmeFilePath = cacheReadmeFilePath;
            ProvisioningTempDirectory = provisioningTempDirectory;
        }

        // テンプレートマップの配置(ServerDataDirectory/map/map.json)を一元定義する
        // Single definition of the template map location (ServerDataDirectory/map/map.json)
        public static string ServerDataMapJsonPath(string serverDataDirectory)
        {
            return Path.Combine(serverDataDirectory, "map", "map.json");
        }

        // 本来形: ワールドディレクトリのルートから全レイアウトを導出する
        // Canonical form: derive the full layout from a world root directory
        public static WorldDataDirectory FromWorldRoot(string worldRootDirectory)
        {
            // 末尾区切りを除去して.provisioningが確定先の内側に潜り込むのを防ぐ
            // Trim any trailing separator so ".provisioning" never nests inside the target root
            var normalizedRoot = worldRootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var cacheDirectory = Path.Combine(normalizedRoot, "cache");
            return new WorldDataDirectory(
                normalizedRoot,
                Path.Combine(normalizedRoot, "world.json"),
                Path.Combine(normalizedRoot, "map.json"),
                Path.Combine(normalizedRoot, "save.json"),
                Path.Combine(normalizedRoot, "terrain"),
                cacheDirectory,
                Path.Combine(cacheDirectory, "README.txt"),
                normalizedRoot + ".provisioning");
        }

        // レガシー形: ワールドディレクトリを持たない構成(テスト427箇所・クライアント早期DI)。
        // mapはServerDataDirectory/map/map.json、saveは明示パス。Root系プロパティはnull
        // Legacy form for DI without a world dir (tests / client early init)
        public static WorldDataDirectory FromServerDataMap(string serverDataDirectory, string saveJsonFilePath)
        {
            return new WorldDataDirectory(
                null,
                null,
                ServerDataMapJsonPath(serverDataDirectory),
                saveJsonFilePath,
                null,
                null,
                null,
                null);
        }
    }
}
