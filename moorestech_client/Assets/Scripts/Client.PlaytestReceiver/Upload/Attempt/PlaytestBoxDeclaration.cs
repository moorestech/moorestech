using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.PlaytestReceiver.Http;
using Newtonsoft.Json;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload.Attempt
{
    // 見送りファイル1件。complete補足のskipped[]に載る
    // One skipped file; goes into the complete supplement's skipped[]
    public sealed class PlaytestSkippedFile
    {
        [JsonProperty("path")] public readonly string Path;
        [JsonProperty("reason")] public readonly string Reason;
        [JsonProperty("bytes")] public readonly long Bytes;

        public PlaytestSkippedFile(string path, string reason, long bytes)
        {
            Path = path;
            Reason = reason;
            Bytes = bytes;
        }
    }

    // 箱の走査から宣言（送るもの）と見送り（送らないものと理由）を作る。送信前に分かる理由と、過去の走行で記録した見送りはすべてここで決まる
    // Builds the declaration (what to send) and the skips (what not to, with reasons); every reason knowable before sending, and the skips recorded by earlier runs, are decided here
    public sealed class PlaytestBoxDeclaration
    {
        public readonly IReadOnlyList<PlaytestDeclaredFile> Files;
        public readonly IReadOnlyList<PlaytestSkippedFile> Skipped;

        // 箱の格付けで必須のファイルが見送られていればその説明、揃っていればnull
        // A description of a file the box's policy ranks required that got skipped, or null when all are present
        public readonly string MissingRequiredReason;

        private readonly IPlaytestBoxFilePolicy _filePolicy;

        private PlaytestBoxDeclaration(IReadOnlyList<PlaytestDeclaredFile> files, IReadOnlyList<PlaytestSkippedFile> skipped, IPlaytestBoxFilePolicy filePolicy)
        {
            Files = files;
            Skipped = skipped;
            _filePolicy = filePolicy;
            var missingRequired = skipped.FirstOrDefault(file => filePolicy.RankOf(file.Path) == PlaytestBundleFileRank.Required);
            MissingRequiredReason = missingRequired == null ? null : $"{missingRequired.Path} was skipped ({missingRequired.Reason})";
        }

        public static PlaytestBoxDeclaration Build(PlaytestOutboxBox box)
        {
            var files = new List<PlaytestDeclaredFile>();
            var skipped = new List<PlaytestSkippedFile>();
            var recordedSkips = PlaytestUploadSkipRecord.Read(box.Directory);
            var earlierDeclaration = PlaytestUploadDeclaredRecord.Read(box.Directory);
            long total = 0;
            // 優先度順（必須→補助→静止画）、同順位は序数順。上限で落ちるのは常に後ろの静止画で、実行ごとにも変わらない
            // Priority order (required, supporting, stills), ordinal within a rank; the caps always drop trailing stills, identically on every run
            var payloads = PlaytestOutboxScanner.ListPayloadFiles(box.Directory)
                .Select(absolute => (Absolute: absolute, Relative: PlaytestOutboxScanner.ToRelativePath(box.Directory, absolute)))
                .OrderBy(file => box.FilePolicy.RankOf(file.Relative))
                .ThenBy(file => file.Relative, StringComparer.Ordinal);
            foreach (var (absolute, relative) in payloads)
            {
                var reason = DescribeSkip(relative, absolute, files.Count, total, out var length);
                if (reason != null)
                {
                    Debug.LogWarning($"[PlaytestReceiver] skipping {relative}: {reason}");
                    skipped.Add(new PlaytestSkippedFile(relative, reason, length));
                    continue;
                }
                files.Add(new PlaytestDeclaredFile(relative, length, absolute));
                total += length;
            }
            return new PlaytestBoxDeclaration(files, skipped, box.FilePolicy);

            #region Internal

            // 過去の走行でR2が拒んだファイルは記録どおり見送り、前回の宣言に無いファイルは入れない（前回上限で落ちた静止画が空いた枠へ入ると宣言の拡大で拒まれる）
            // Files R2 refused earlier stay skipped as recorded, and files outside the last declaration stay out (a still the caps dropped last time would grow the declaration and be refused)
            // それ以外の判定は PlaytestUploadPath.DescribeRejection 一本（受け口の parseDeclaration と同じ規則）
            // Every other rule lives in PlaytestUploadPath.DescribeRejection (mirroring the receiver's parseDeclaration)
            string DescribeSkip(string relative, string absolute, int declaredCount, long declaredTotal, out long length)
            {
                length = new FileInfo(absolute).Length;
                if (recordedSkips.TryGetValue(relative, out var recorded)) return recorded;
                if (earlierDeclaration != null && !earlierDeclaration.Paths.Contains(relative)) return "outside-earlier-declaration";
                return PlaytestUploadPath.DescribeRejection(relative, length, declaredCount, declaredTotal);
            }

            #endregion
        }

        // 送信後に分かった1ファイルの見送り。宣言から外し見送りへ積んだ新しい宣言を返す（受け口は縮小した再宣言だけを受け付ける）
        // A skip learned after sending; returns a new declaration with the file moved from the files to the skips (the receiver accepts only a shrunk re-declaration)
        public PlaytestBoxDeclaration WithSkipped(string path, string reason)
        {
            var remaining = new List<PlaytestDeclaredFile>();
            var skipped = new List<PlaytestSkippedFile>(Skipped);
            foreach (var file in Files)
            {
                if (file.Path == path) skipped.Add(new PlaytestSkippedFile(path, reason, file.Bytes));
                else remaining.Add(file);
            }
            return new PlaytestBoxDeclaration(remaining, skipped, _filePolicy);
        }
    }
}
