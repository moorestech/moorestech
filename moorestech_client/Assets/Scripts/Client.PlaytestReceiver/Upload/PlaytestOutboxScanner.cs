using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.PlaytestReceiver.Http;

namespace Client.PlaytestReceiver.Upload
{
    // outboxの走査と印の名前の正本。どの箱が未送で、箱の中の何を送るのかはここだけが決める
    // Owns the outbox scan and the marker names; which boxes are pending and what inside them ships is decided only here
    public static class PlaytestOutboxScanner
    {
        // READYはバグ報告の書き出し側（BugReportOutbox）が打つ印。別アセンブリなので名前の一致はテストで固定する
        // READY is written by the bug report side (BugReportOutbox); it lives in another assembly, so a test pins the match
        public const string ReadyMarker = "READY";
        public const string UploadedMarker = "UPLOADED";
        public const string FailedMarker = "UPLOAD_FAILED";
        public const string AttemptsMarker = "UPLOAD_ATTEMPTS";

        private static readonly string[] Markers = { ReadyMarker, UploadedMarker, FailedMarker, AttemptsMarker };

        public static IReadOnlyList<PlaytestOutboxBox> ScanPending(string reportOutbox, string progressOutbox)
        {
            var boxes = new List<PlaytestOutboxBox>();
            boxes.AddRange(ScanOne(reportOutbox, PlaytestUploadKind.Report));
            boxes.AddRange(ScanOne(progressOutbox, PlaytestUploadKind.Progress));

            // 箱のIDは yyyyMMdd_HHmmss_<hex> なので辞書順が時刻順になる。古い順に送る
            // Bundle ids are yyyyMMdd_HHmmss_<hex>, so lexicographic order is chronological; ship oldest first
            return boxes.OrderBy(box => box.BundleId, StringComparer.Ordinal).ToList();

            #region Internal

            IEnumerable<PlaytestOutboxBox> ScanOne(string outbox, PlaytestUploadKind kind)
            {
                if (!Directory.Exists(outbox)) yield break;

                foreach (var directory in Directory.GetDirectories(outbox))
                {
                    if (!File.Exists(Path.Combine(directory, ReadyMarker))) continue;
                    if (File.Exists(Path.Combine(directory, UploadedMarker))) continue;
                    if (File.Exists(Path.Combine(directory, FailedMarker))) continue;
                    yield return new PlaytestOutboxBox(directory, Path.GetFileName(directory), kind);
                }
            }

            #endregion
        }

        public static IReadOnlyList<string> ListPayloadFiles(string boxDirectory)
        {
            // 印は箱の直下にしか置かれない。名前だけで判定すると logs/READY のような中身まで落ちる
            // Markers live only at the box root; matching on the name alone would also drop payloads such as logs/READY
            return Directory.GetFiles(boxDirectory, "*", SearchOption.AllDirectories)
                .Where(path => !Markers.Contains(ToRelativePath(boxDirectory, path)))
                .ToList();
        }

        public static string ToRelativePath(string boxDirectory, string filePath)
        {
            var relative = filePath.Substring(boxDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
