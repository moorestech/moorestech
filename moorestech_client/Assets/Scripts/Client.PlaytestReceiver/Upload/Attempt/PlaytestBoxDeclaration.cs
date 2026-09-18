using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.PlaytestReceiver.Http;
using Newtonsoft.Json;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload.Attempt
{
    // 見送ったファイル1件。completeの補足（skipped[]）にそのまま載る
    // One skipped file; rides in the complete supplement's skipped[] as is
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

    // 箱の走査から宣言（送るもの）と見送り（送らないものと理由）を作る。送信前に分かる理由はすべてここで決まる
    // Builds the declaration (what to send) and the skips (what not to, with reasons) from the box; every reason knowable before sending is decided here
    public sealed class PlaytestBoxDeclaration
    {
        public readonly IReadOnlyList<PlaytestDeclaredFile> Files;
        public readonly IReadOnlyList<PlaytestSkippedFile> Skipped;

        private PlaytestBoxDeclaration(IReadOnlyList<PlaytestDeclaredFile> files, IReadOnlyList<PlaytestSkippedFile> skipped)
        {
            Files = files;
            Skipped = skipped;
        }

        public static PlaytestBoxDeclaration Build(PlaytestOutboxBox box)
        {
            var files = new List<PlaytestDeclaredFile>();
            var skipped = new List<PlaytestSkippedFile>();
            long total = 0;
            // 走査順はOS任せなので序数順に揃える。件数・総量の上限でどれが見送られるかを実行ごとに変えない
            // Directory enumeration order is OS-defined, so sort ordinally; which files the count/total caps skip must not vary between runs
            var payloads = PlaytestOutboxScanner.ListPayloadFiles(box.Directory).OrderBy(path => path, StringComparer.Ordinal);
            foreach (var absolute in payloads)
            {
                var relative = PlaytestOutboxScanner.ToRelativePath(box.Directory, absolute);
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
            return new PlaytestBoxDeclaration(files, skipped);

            #region Internal

            // 見送り理由の判定は PlaytestUploadPath.DescribeRejection 一本（受け口の parseDeclaration と同じ規則）。ここでは長さを測って渡すだけ
            // The rejection rules live in PlaytestUploadPath.DescribeRejection alone (mirroring the receiver's parseDeclaration); this only measures the length
            static string DescribeSkip(string relative, string absolute, int declaredCount, long declaredTotal, out long length)
            {
                length = new FileInfo(absolute).Length;
                return PlaytestUploadPath.DescribeRejection(relative, length, declaredCount, declaredTotal);
            }

            #endregion
        }
    }
}
