using System.Collections.Generic;
using System.IO;

namespace Client.PlaytestReceiver.Upload
{
    // 送信後にR2が4xxで拒んだファイルの見送り記録。受け口は縮小した宣言を元へ戻せないので、次回起動以降も同じファイルを見送り続ける
    // The record of files R2 refused with a 4xx after sending; the receiver never lets a shrunk declaration grow back, so later runs keep skipping them
    public static class PlaytestUploadSkipRecord
    {
        // 1行1件「path<TAB>reason」。宣言規則が制御文字を拒むのでpathにTABは入らない
        // One "path<TAB>reason" per line; the declaration rules refuse control characters, so a path never holds a TAB
        public static void Append(string boxDirectory, string path, string reason)
        {
            File.AppendAllText(Path.Combine(boxDirectory, PlaytestOutboxScanner.SkippedMarker), $"{path}\t{reason}\n");
        }

        public static IReadOnlyDictionary<string, string> Read(string boxDirectory)
        {
            var recorded = new Dictionary<string, string>();
            var markerPath = Path.Combine(boxDirectory, PlaytestOutboxScanner.SkippedMarker);
            if (!File.Exists(markerPath)) return recorded;
            foreach (var line in File.ReadAllLines(markerPath))
            {
                var tab = line.IndexOf('\t');
                if (tab <= 0) continue;
                recorded[line.Substring(0, tab)] = line.Substring(tab + 1);
            }
            return recorded;
        }
    }
}
