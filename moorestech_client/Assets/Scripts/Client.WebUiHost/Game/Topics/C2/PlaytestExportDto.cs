using System;
using System.Collections.Generic;
using System.Linq;
using Client.PlaytestReceiver.Recovery;

namespace Client.WebUiHost.Game.Topics
{
    // 書き出し状態ごとの値だけを送り、null値はWebUiJsonで省略する
    // Send only the fields of each export state; WebUiJson omits null fields
    public sealed class PlaytestExportDto
    {
        public readonly string Kind;
        public readonly string ZipPath;
        public readonly int? BoxCount;
        public readonly List<PlaytestExportMissingDto> Missing;
        public readonly bool? Revealed;
        public readonly string Reason;

        private PlaytestExportDto(string kind, string zipPath, int? boxCount, List<PlaytestExportMissingDto> missing, bool? revealed, string reason)
        { Kind = kind; ZipPath = zipPath; BoxCount = boxCount; Missing = missing; Revealed = revealed; Reason = reason; }

        internal static PlaytestExportDto From(PlaytestExportStatus status)
        {
            return status switch
            {
                PlaytestExportIdle => new PlaytestExportDto("idle", null, null, null, null, null),
                PlaytestExportRunning => new PlaytestExportDto("running", null, null, null, null, null),
                PlaytestExportDone done => new PlaytestExportDto("done", done.ZipPath, done.BoxCount, done.Missing.Select(m => new PlaytestExportMissingDto(m.Item, m.Reason)).ToList(), done.Revealed, null),
                PlaytestExportFailed failed => new PlaytestExportDto("failed", null, null, null, null, failed.Reason),
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown export status"),
            };
        }
    }

    public sealed class PlaytestExportMissingDto
    {
        public readonly string Item;
        public readonly string Reason;
        public PlaytestExportMissingDto(string item, string reason) { Item = item; Reason = reason; }
    }
}
