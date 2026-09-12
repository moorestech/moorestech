using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.Capture;
using Game.MapGeneration.Export;
using Game.Paths;
using Newtonsoft.Json;

namespace Client.Game.InGame.BugReport
{
    // 退避済みのスナップショットと記録時のワールド定義を箱へ入れる。再現側はこの2つが揃って初めて起動できる
    // Puts the staged snapshots and the recording's world definition into the box; reproduction boots only when both are present
    public static class BugReportWorldFilesCopier
    {
        public const string SnapshotDirectoryName = "snapshots";
        public const string WorldDirectoryName = "world";
        private const string TerrainDirectoryName = "terrain";
        private const string GeneratedMapMode = "generated";

        public static void Copy(BugReportCapturedData data, string bundleDirectory, BugReportManifest manifest)
        {
            CopyStagedSnapshots(data, bundleDirectory, manifest);
            CopyWorldDefinition(data, bundleDirectory, manifest);
        }

        private static void CopyStagedSnapshots(BugReportCapturedData data, string bundleDirectory, BugReportManifest manifest)
        {
            if (string.IsNullOrEmpty(data.StagedSnapshotDirectory))
            {
                manifest.AddMissing(SnapshotDirectoryName, "Escape時点のサーバー記録を退避できていなかった");
                return;
            }

            var snapshots = Path.Combine(bundleDirectory, SnapshotDirectoryName);
            Directory.CreateDirectory(snapshots);

            // 箱に入った実体だけを manifest に載せる。名前だけ載せると受け側が存在しないスナップショットを土台にする
            // Only the files that really landed in the box go into the manifest; names alone make the receiver build on a snapshot that is not there
            manifest.SnapshotFiles = CopyStagedFiles(data.StagedSnapshotDirectory, snapshots, data.SnapshotFileNames, manifest);
            manifest.PacketLogFiles = CopyStagedFiles(data.StagedSnapshotDirectory, snapshots, data.PacketLogFileNames, manifest);

            var ticks = new List<ulong>();
            foreach (var name in manifest.SnapshotFiles)
            {
                // ファイル名規則の定義元は WorldDataDirectory 1箇所。読み側も同じ定義に委譲している
                // WorldDataDirectory is the single definition of the naming rule, and the reading side delegates to the same one
                if (WorldDataDirectory.TryParseSnapshotTick(name, out var tick)) ticks.Add(tick);
                else manifest.AddMissing(name, "スナップショットのtickを読み取れないファイル名だった");
            }
            ticks.Sort();
            manifest.SnapshotTicks = ticks;
        }

        private static List<string> CopyStagedFiles(string stagedDirectory, string destinationDirectory, IReadOnlyList<string> fileNames, BugReportManifest manifest)
        {
            var copied = new List<string>();
            foreach (var name in fileNames)
            {
                var source = Path.Combine(stagedDirectory, name);
                if (!File.Exists(source))
                {
                    manifest.AddMissing(name, "確保時の退避先に無かった");
                    continue;
                }
                File.Copy(source, Path.Combine(destinationDirectory, name), true);
                copied.Add(name);
            }
            return copied;
        }

        // 再現にはスナップショット本体だけでなく、その隣のワールド定義（地図・世界メタ・地形）が要る
        // Reproduction needs the world definition beside the snapshots (map, world meta and terrain), not just the snapshots
        private static void CopyWorldDefinition(BugReportCapturedData data, string bundleDirectory, BugReportManifest manifest)
        {
            if (string.IsNullOrEmpty(data.WorldRootDirectory))
            {
                manifest.AddMissing(WorldDirectoryName, "記録時のワールドディレクトリが分からなかった");
                return;
            }

            var source = WorldDataDirectory.FromWorldRoot(data.WorldRootDirectory);
            var world = Path.Combine(bundleDirectory, WorldDirectoryName);
            var destination = WorldDataDirectory.FromWorldRoot(world);
            Directory.CreateDirectory(world);
            CopyIfExists(source.WorldMetaFilePath, destination.WorldMetaFilePath, manifest);
            CopyIfExists(source.MapJsonFilePath, destination.MapJsonFilePath, manifest);
            CopyTerrain(source, destination, manifest);
        }

        // 生成ワールドの起動は terrain の実ファイルをバイト数まで数えるので、1枚でも欠けると受け側が例外で落ちる
        // Booting a generated world counts the terrain files down to their bytes, so one missing file kills the receiving side
        private static void CopyTerrain(WorldDataDirectory source, WorldDataDirectory destination, BugReportManifest manifest)
        {
            var requiresTerrain = RequiresTerrain(source.WorldMetaFilePath, manifest);
            if (!Directory.Exists(source.TerrainDirectory))
            {
                if (requiresTerrain) manifest.AddMissing(TerrainDirectoryName, "生成ワールドなのに地形ディレクトリが無かった");
                return;
            }

            Directory.CreateDirectory(destination.TerrainDirectory);
            var copied = 0;
            foreach (var path in Directory.GetFiles(source.TerrainDirectory))
            {
                File.Copy(path, Path.Combine(destination.TerrainDirectory, Path.GetFileName(path)), true);
                copied++;
            }
            if (copied == 0 && requiresTerrain) manifest.AddMissing(TerrainDirectoryName, "生成ワールドなのに地形ファイルが1枚も無かった");
        }

        // world.json は外部入力のJSON。読めないときは地形を必須扱いにして、欠けていることが箱に残るようにする
        // world.json is external JSON input; when it cannot be read, terrain is treated as required so its absence still lands in the box
        private static bool RequiresTerrain(string worldMetaFilePath, BugReportManifest manifest)
        {
            if (!File.Exists(worldMetaFilePath)) return false;
            try
            {
                var meta = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(worldMetaFilePath));
                return string.Equals(meta?.MapMode, GeneratedMapMode, System.StringComparison.OrdinalIgnoreCase);
            }
            catch (JsonException e)
            {
                manifest.AddMissing("world.json", $"mapModeを読み取れなかった: {e.Message}");
                return true;
            }
        }

        private static void CopyIfExists(string sourcePath, string destinationPath, BugReportManifest manifest)
        {
            if (!File.Exists(sourcePath))
            {
                manifest.AddMissing(Path.GetFileName(sourcePath), "ワールドディレクトリに無かった");
                return;
            }
            File.Copy(sourcePath, destinationPath, true);
        }
    }
}
