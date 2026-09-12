using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Client.Game.InGame.BugReport.Recording
{
    // 区間ファイルのリング管理。ffmpegは再起動のたびlive側をseg_00から上書きするため確定分をretainedへ退避する
    // Manages the segment ring; every ffmpeg restart rewrites live from seg_00, so finished ones move into retained
    public static class RecordingSegmentRing
    {
        public const string SegmentSearchPattern = "seg_*.mp4";
        private const string SegmentPrefix = "seg_";

        // live にある確定済み区間を retained へ移し、新しい世代の上書きから守ったうえで保持秒数まで間引く
        // Moves finished live segments into retained, out of the next generation's way, and trims to the retention window
        public static void PromoteCompletedSegments(string liveDirectory, string retainedDirectory, double keepSeconds)
        {
            if (!Directory.Exists(liveDirectory))
            {
                Debug.LogWarning($"録画区間の退避を見送りました（liveディレクトリがありません）: {liveDirectory}");
                return;
            }
            Directory.CreateDirectory(retainedDirectory);

            var sequence = NextSequence(retainedDirectory);
            foreach (var file in OrderedSegments(liveDirectory))
            {
                File.Move(file.FullName, Path.Combine(retainedDirectory, $"{SegmentPrefix}{sequence:D6}.mp4"));
                sequence++;
            }

            // 書き込み途中で長さ0のまま残った区間は中身が無いので捨てる
            // Segments left at zero length mid-write hold nothing, so drop them
            foreach (var leftover in Directory.GetFiles(liveDirectory, SegmentSearchPattern)) File.Delete(leftover);

            TrimToRetentionWindow(retainedDirectory, keepSeconds);
        }

        // 本数ではなく確定時刻の差分（＝各区間の実尺）を積算し、保持秒数を下回らない最小本数まで間引く
        // Accumulates gaps between finalize times (each segment's actual duration), trimming to the minimum count that keeps the retention window
        private static void TrimToRetentionWindow(string retainedDirectory, double keepSeconds)
        {
            var retained = OrderedSegments(retainedDirectory).ToList();
            if (retained.Count == 0) return;

            var accumulatedSeconds = 0.0;
            var oldestKeptIndex = 0;
            for (var index = retained.Count - 1; index > 0; index--)
            {
                accumulatedSeconds += (retained[index].LastWriteTimeUtc - retained[index - 1].LastWriteTimeUtc).TotalSeconds;
                oldestKeptIndex = index - 1;
                if (accumulatedSeconds >= keepSeconds) break;
            }

            for (var i = 0; i < oldestKeptIndex; i++) File.Delete(retained[i].FullName);
        }

        // 古い→新しいの順に並べる。excludeNewestLive のときは ffmpeg が書き込み中の最新live区間を外す
        // Orders oldest to newest; excludeNewestLive drops the newest live segment that ffmpeg is still writing
        public static IReadOnlyList<string> ListInOrder(string retainedDirectory, string liveDirectory, bool excludeNewestLive)
        {
            var files = new List<string>();
            if (Directory.Exists(retainedDirectory)) files.AddRange(OrderedSegments(retainedDirectory).Select(file => file.FullName));
            if (!Directory.Exists(liveDirectory)) return files;

            var live = OrderedSegments(liveDirectory).Select(file => file.FullName).ToList();
            if (excludeNewestLive && live.Count > 0) live.RemoveAt(live.Count - 1);
            files.AddRange(live);
            return files;
        }

        private static IEnumerable<FileInfo> OrderedSegments(string directory)
        {
            return Directory.GetFiles(directory, SegmentSearchPattern).Select(path => new FileInfo(path))
                .Where(file => file.Length > 0).OrderBy(file => file.LastWriteTimeUtc).ToList();
        }

        private static int NextSequence(string retainedDirectory)
        {
            var next = 0;
            foreach (var path in Directory.GetFiles(retainedDirectory, SegmentSearchPattern))
            {
                var name = Path.GetFileNameWithoutExtension(path).Substring(SegmentPrefix.Length);
                if (!int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var value)) continue;
                if (value >= next) next = value + 1;
            }
            return next;
        }
    }
}
