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
        // 置き場の名前は書き側と共有する定義元から取る。ここで再定義すると片方の改名で再現だけが無言で全滅する
        // The directory names come from the definition shared with the writer; redefining them here lets a one-sided rename kill only the reproduction, silently
        private const string SnapshotDirectoryName = BugReportBundleLayout.SnapshotDirectoryName;
        private const string WorldDirectoryName = BugReportBundleLayout.WorldDirectoryName;

        // 区間を覆うパケットが1件も無かったペアの印。不一致の理由が「ログの欠け」なのか非決定性なのかを読み手が分ける
        // Marks a pair whose interval no packet covered, so the reader can tell a missing log from real non-determinism
        private const string NoPacketsInRangeCoverage = "no_packets_in_range";
        private const string CoveredCoverage = "covered";

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

            // 記録時と違うサーバーデータで再生すると、マスタローダーが data[NN] のような読み解けない例外で落ちる
            // Replaying with server data other than the recording's dies in the master loader with an unreadable exception like data[NN]
            var mismatch = BundleServerDataCheck.FindMismatch(bundleDirectory, serverDataDirectory);
            if (mismatch != null) return Reject(mismatch);

            var snapshotFiles = WorldDataDirectory.EnumerateSnapshotFiles(snapshotDirectory);
            if (snapshotFiles.Count < 2) return Reject($"スナップショットが{snapshotFiles.Count}枚しかなく隣接区間を作れません dir:{snapshotDirectory}");

            var segments = WorldDataDirectory.EnumeratePacketLogFiles(snapshotDirectory);
            if (segments.Count == 0) Debug.LogWarning($"パケットログ区間が1件もありません。パケット0件の再生になります dir:{snapshotDirectory}");

            var sourceWorld = WorldDataDirectory.FromWorldRoot(worldRoot);
            var pairs = new JArray();
            var allEqual = true;
            var noPacketsInRangePairs = 0;
            for (var i = 0; i + 1 < snapshotFiles.Count; i++)
            {
                var from = TickOf(snapshotFiles[i]);
                var to = TickOf(snapshotFiles[i + 1]);
                var result = SnapshotReplayer.Replay(new ReplayRequest(serverDataDirectory, sourceWorld, snapshotFiles[i], segments, to));
                var comparison = SnapshotJsonComparer.Compare(File.ReadAllText(snapshotFiles[i + 1]), result.SnapshotJson);
                allEqual &= comparison.Equal;
                if (result.InRangePacketCount == 0) noPacketsInRangePairs++;

                // 区間を覆うログが無いペアは不一致でも非決定性ではない。読み手が区別できるよう件数と被覆状態を必ず残す
                // A pair with no log covering its interval is not non-determinism even when unequal, so record the counts and coverage
                pairs.Add(new JObject
                {
                    ["from"] = from,
                    ["to"] = to,
                    ["equal"] = comparison.Equal,
                    ["replayedPackets"] = result.ReplayedPacketCount,
                    ["inRangePackets"] = result.InRangePacketCount,
                    ["excludedPackets"] = result.ExcludedPacketCount,
                    ["coverage"] = result.InRangePacketCount == 0 ? NoPacketsInRangeCoverage : CoveredCoverage,
                    ["differences"] = new JArray(comparison.Differences),
                });
            }

            // 被覆の欠けは戻り値にも出す。JSON を開かない呼び出し側が不一致を非決定性と決めつけないため
            // The coverage shortfall also goes into the return value, so a caller that never opens the JSON does not call a mismatch non-determinism
            if (noPacketsInRangePairs > 0) Debug.LogWarning($"区間を覆うパケットが無いペアが {noPacketsInRangePairs}/{pairs.Count} 件あります。不一致でも非決定性とは限りません bundle:{bundleDirectory}");

            var document = new JObject
            {
                ["allEqual"] = allEqual,
                ["noPacketsInRangePairs"] = noPacketsInRangePairs,
                ["pairs"] = pairs,
            };
            File.WriteAllText(Path.Combine(bundleDirectory, "replay-check.json"), document.ToString());
            return $"replay-check.json written: allEqual={allEqual} pairs={pairs.Count} noPacketsInRange={noPacketsInRangePairs}";
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
