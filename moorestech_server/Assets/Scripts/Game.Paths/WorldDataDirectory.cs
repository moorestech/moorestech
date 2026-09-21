using System;
using System.Collections.Generic;
using System.Globalization;
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
        public readonly string TerrainVisualDirectory;
        public string CacheDirectory { get; }
        public string CacheReadmeFilePath { get; }
        public string ProvisioningTempDirectory { get; }

        // 常時記録の置き場。セーブファイルの隣に置き、セーブごと持ち出せるようにする
        // Where always-on capture lives: beside the save file so a bundle can carry both
        public string SnapshotDirectory { get; }
        public const string SnapshotOwnerFileName = "capture-owner.json";

        // マイグレーション前の原本と、マスタ欠損で除去したデータの置き場。どちらもセーブファイルの隣
        // Where pre-migration originals and data pruned for missing masters live: both beside the save file
        public string SaveBackupDirectory { get; }
        public string SavePrunedDirectory { get; }

        private WorldDataDirectory(string root, string worldMetaFilePath, string mapJsonFilePath, string saveJsonFilePath,
            string terrainDirectory, string terrainVisualDirectory, string cacheDirectory, string cacheReadmeFilePath,
            string provisioningTempDirectory)
        {
            Root = root;
            WorldMetaFilePath = worldMetaFilePath;
            MapJsonFilePath = mapJsonFilePath;
            SaveJsonFilePath = saveJsonFilePath;
            TerrainDirectory = terrainDirectory;
            TerrainVisualDirectory = terrainVisualDirectory;
            CacheDirectory = cacheDirectory;
            CacheReadmeFilePath = cacheReadmeFilePath;
            ProvisioningTempDirectory = provisioningTempDirectory;
            SnapshotDirectory = saveJsonFilePath == null ? null : Path.Combine(Path.GetDirectoryName(saveJsonFilePath), "snapshots");
            SaveBackupDirectory = saveJsonFilePath == null ? null : Path.Combine(Path.GetDirectoryName(saveJsonFilePath), "backup");
            SavePrunedDirectory = saveJsonFilePath == null ? null : Path.Combine(Path.GetDirectoryName(saveJsonFilePath), "pruned");
        }

        // スナップショットとパケットログはセーブファイルの隣の snapshots/ に置く。ファイル名規則の定義はここだけ
        // Snapshots and packet logs live in snapshots/ beside the save file; the naming rule lives only here
        private const string SnapshotFilePrefix = "tick_";
        private const string SnapshotFileExtension = ".json";
        private const string PacketLogFilePrefix = "packets_";
        private const string PacketLogFileExtension = ".bin";
        public const string SnapshotFileSearchPattern = SnapshotFilePrefix + "*" + SnapshotFileExtension;
        public const string PacketLogFileSearchPattern = PacketLogFilePrefix + "*" + PacketLogFileExtension;

        public string SnapshotFilePath(ulong tick)
        {
            return Path.Combine(SnapshotDirectory, SnapshotFileName(tick));
        }

        public static string SnapshotFileName(ulong tick)
        {
            return $"{SnapshotFilePrefix}{tick}{SnapshotFileExtension}";
        }

        public static string ReceivedPacketLogFileName(ulong fromTick)
        {
            return $"{PacketLogFilePrefix}{fromTick}{PacketLogFileExtension}";
        }

        public static bool TryParseSnapshotTick(string fileName, out ulong tick)
        {
            return TickSuffixedFileNames.TryParseTick(fileName, SnapshotFilePrefix, SnapshotFileExtension, out tick);
        }

        public static bool TryParsePacketLogFromTick(string fileName, out ulong fromTick)
        {
            return TickSuffixedFileNames.TryParseTick(fileName, PacketLogFilePrefix, PacketLogFileExtension, out fromTick);
        }

        // 置き場のスナップショット／区間ファイルをtick昇順で返す
        // List the snapshot / segment files in tick order
        public static IReadOnlyList<string> EnumerateSnapshotFiles(string snapshotDirectory)
        {
            return TickSuffixedFileNames.EnumerateByTick(snapshotDirectory, SnapshotFilePrefix, SnapshotFileExtension);
        }

        public static IReadOnlyList<string> EnumeratePacketLogFiles(string snapshotDirectory)
        {
            return TickSuffixedFileNames.EnumerateByTick(snapshotDirectory, PacketLogFilePrefix, PacketLogFileExtension);
        }

        // 版ごとに1本だけ原本を残す。後から置換・返金のマイグレーションを足すとき遡れる単位
        // Keeps one original per version, the unit a later replace/refund migration can go back to
        public string BackupSaveJsonPath(int worldVersion)
        {
            return Path.Combine(SaveBackupDirectory, worldVersion.ToString(CultureInfo.InvariantCulture), "save.json");
        }

        // 拡張ISO形式のコロンはWindowsのファイル名に使えないので基本形式で、年が化けないようInvariantCultureで綴る
        // The extended ISO colons are invalid in Windows file names, so the basic form is spelled with InvariantCulture
        public string PrunedJsonPath(DateTime utcNow, int collisionIndex)
        {
            var stamp = utcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
            var name = collisionIndex == 0 ? $"{stamp}.json" : $"{stamp}-{collisionIndex}.json";
            return Path.Combine(SavePrunedDirectory, name);
        }

        // タイル座標からterrainバイナリのパスを導出する。ファイル名規則の定義はここだけに置く
        // Derive terrain binary paths from tile coordinates; the naming rule lives only here
        public string TerrainHeightFilePath(int tileX, int tileZ)
        {
            return Path.Combine(TerrainDirectory, $"height_{tileX}_{tileZ}.r16");
        }

        // 高さから再構築できる見た目(splatmap/detail)の置き場。terrainとは別に消せるよう分けてある
        // Holds the visuals (splatmap/detail) rebuildable from heights, kept apart from terrain so it can be dropped alone
        public string TerrainVisualCacheFilePath(int tileX, int tileZ)
        {
            return Path.Combine(TerrainVisualDirectory, $"visual_{tileX}_{tileZ}.bin");
        }

        // テンプレートマップの配置(ServerDataDirectory/map/map.json)を一元定義する
        // Single definition of the template map location (ServerDataDirectory/map/map.json)
        public static string ServerDataMapJsonPath(string serverDataDirectory)
        {
            return Path.Combine(serverDataDirectory, "map", "map.json");
        }

        // ビルドに同梱された生成済みワールドのスナップショット置き場。共有キャッシュと同じレイアウトでゲームデータ内に置く
        // Where a build ships a pre-generated world snapshot; it sits inside the game data with the shared cache's layout
        public static WorldDataDirectory ForBundledSnapshot(string serverDataDirectory, string worldId)
        {
            return FromWorldRoot(Path.Combine(serverDataDirectory, "worldSnapshots", worldId));
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
                Path.Combine(normalizedRoot, "visual"),
                cacheDirectory,
                Path.Combine(cacheDirectory, "README.txt"),
                normalizedRoot + ".provisioning");
        }

        // セーブだけ別の場所から読む構成。再生がバンドルの map/terrain を使いつつ、スナップショットを save として読むために使う
        // Same world layout with the save read from elsewhere; replay uses the bundle's map/terrain while loading a snapshot as the save
        public WorldDataDirectory WithSaveJsonFilePath(string saveJsonFilePath)
        {
            return new WorldDataDirectory(Root, WorldMetaFilePath, MapJsonFilePath, saveJsonFilePath,
                TerrainDirectory, TerrainVisualDirectory, CacheDirectory, CacheReadmeFilePath, ProvisioningTempDirectory);
        }

        // 同一PCで先焼きとクライアント焼きが共有するワールドキャッシュ。worldIdからの導出はここだけが持つ
        // The world cache shared by the prebake and the client bake on one PC; deriving it from a worldId lives only here
        public static WorldDataDirectory ForWorldCache(string worldId)
        {
            return FromWorldRoot(GameSystemPaths.GetWorldCacheDirectory(worldId));
        }

        // 共有キャッシュのレイアウトを作らずに導出する。有無を調べるだけの参照側が使う
        // Derives the shared cache layout without creating it; for callers that only check whether it exists
        public static WorldDataDirectory ForWorldCacheWithoutCreating(string worldId)
        {
            return FromWorldRoot(GameSystemPaths.GetWorldCacheDirectoryPathWithoutCreating(worldId));
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
                null,
                null);
        }
    }
}
