using System.IO;
using Game.Paths;
using Game.SaveLoad.Snapshot;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Server.Boot.Replay
{
    // バンドル1箱に対する再現ツールの入口。EDC スニペットはこれを1行呼ぶだけにする
    // Entry points of the reproduction tools for one bundle; EDC snippets call these in one line
    public static class BugReportBundleTools
    {
        private const string SnapshotDirectoryName = "snapshots";
        private const string WorldDirectoryName = "world";

        public static string DumpPackets(string bundleDirectory)
        {
            var snapshotDirectory = Path.Combine(bundleDirectory, SnapshotDirectoryName);
            if (!Directory.Exists(snapshotDirectory)) return Reject($"バンドルに {SnapshotDirectoryName}/ がありません bundle:{bundleDirectory}");

            var segments = WorldDataDirectory.EnumeratePacketLogFiles(snapshotDirectory);
            if (segments.Count == 0) return Reject($"パケットログ区間が1件もありません dir:{snapshotDirectory}");

            var outputPath = Path.Combine(bundleDirectory, "packets.jsonl");
            var count = PacketLogJsonDumper.Dump(segments, outputPath);
            return $"packets.jsonl written: {count} records from {segments.Count} segments";
        }

        // スナップショット間を順に再生し、隣同士が一致するかを replay-check.json に残す
        // Replays each adjacent snapshot interval and records whether the pairs match in replay-check.json
        public static string ReplayCheck(string bundleDirectory, string serverDataDirectory)
        {
            var snapshotDirectory = Path.Combine(bundleDirectory, SnapshotDirectoryName);
            var worldRoot = Path.Combine(bundleDirectory, WorldDirectoryName);
            if (!Directory.Exists(snapshotDirectory)) return Reject($"バンドルに {SnapshotDirectoryName}/ がありません bundle:{bundleDirectory}");

            // 再生は記録時のワールド（map.json）を読む。無い箱を template で代用すると instanceId がずれて偽の差分になる
            // Replay reads the recording's own world (map.json); substituting the template shifts instance ids and fabricates differences
            if (!Directory.Exists(worldRoot)) return Reject($"バンドルに {WorldDirectoryName}/ がありません（記録時のワールドが無いと再生は成立しません） bundle:{bundleDirectory}");

            var snapshotFiles = WorldDataDirectory.EnumerateSnapshotFiles(snapshotDirectory);
            if (snapshotFiles.Count < 2) return Reject($"スナップショットが{snapshotFiles.Count}枚しかなく隣接区間を作れません dir:{snapshotDirectory}");

            var segments = WorldDataDirectory.EnumeratePacketLogFiles(snapshotDirectory);
            if (segments.Count == 0) Debug.LogWarning($"パケットログ区間が1件もありません。パケット0件の再生になります dir:{snapshotDirectory}");

            var sourceWorld = WorldDataDirectory.FromWorldRoot(worldRoot);
            var pairs = new JArray();
            var allEqual = true;
            for (var i = 0; i + 1 < snapshotFiles.Count; i++)
            {
                var from = TickOf(snapshotFiles[i]);
                var to = TickOf(snapshotFiles[i + 1]);
                var result = SnapshotReplayer.Replay(new ReplayRequest(serverDataDirectory, sourceWorld, snapshotFiles[i], segments, to));
                var comparison = SnapshotJsonComparer.Compare(File.ReadAllText(snapshotFiles[i + 1]), result.SnapshotJson);
                allEqual &= comparison.Equal;
                pairs.Add(new JObject
                {
                    ["from"] = from,
                    ["to"] = to,
                    ["equal"] = comparison.Equal,
                    ["replayedPackets"] = result.ReplayedPacketCount,
                    ["differences"] = new JArray(comparison.Differences),
                });
            }

            File.WriteAllText(Path.Combine(bundleDirectory, "replay-check.json"), new JObject { ["allEqual"] = allEqual, ["pairs"] = pairs }.ToString());
            return $"replay-check.json written: allEqual={allEqual} pairs={pairs.Count}";
        }

        private static ulong TickOf(string snapshotFilePath)
        {
            WorldDataDirectory.TryParseSnapshotTick(Path.GetFileName(snapshotFilePath), out var tick);
            return tick;
        }

        // 実行できない理由は呼び出し元（EDCの戻り値）と開発者ログの両方へ出す。無言で空の結果を返さない
        // The reason for not running goes to both the caller (the EDC return value) and the developer log; never return an empty result silently
        private static string Reject(string reason)
        {
            Debug.LogError($"バグ報告バンドルの再現ツールを実行できません: {reason}");
            return $"ERROR: {reason}";
        }
    }
}
