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

        // 本来形: ワールドディレクトリのルートから全レイアウトを導出する
        // Canonical form: derive the full layout from a world root directory
        public static WorldDataDirectory FromWorldRoot(string worldRootDirectory)
        {
            var cacheDirectory = Path.Combine(worldRootDirectory, "cache");
            return new WorldDataDirectory(
                worldRootDirectory,
                Path.Combine(worldRootDirectory, "world.json"),
                Path.Combine(worldRootDirectory, "map.json"),
                Path.Combine(worldRootDirectory, "save.json"),
                Path.Combine(worldRootDirectory, "terrain"),
                cacheDirectory,
                Path.Combine(cacheDirectory, "README.txt"),
                worldRootDirectory + ".provisioning");
        }

        // レガシー形: ワールドディレクトリを持たない構成(テスト427箇所・クライアント早期DI)。
        // mapはServerDataDirectory/map/map.json、saveは明示パス。Root系プロパティはnull
        // Legacy form for DI without a world dir (tests / client early init)
        public static WorldDataDirectory FromServerDataMap(string serverDataDirectory, string saveJsonFilePath)
        {
            return new WorldDataDirectory(
                null,
                null,
                Path.Combine(serverDataDirectory, "map", "map.json"),
                saveJsonFilePath,
                null,
                null,
                null,
                null);
        }
    }
}
