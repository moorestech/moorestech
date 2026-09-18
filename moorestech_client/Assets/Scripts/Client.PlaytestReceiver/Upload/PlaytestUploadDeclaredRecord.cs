using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.PlaytestReceiver.Http;

namespace Client.PlaytestReceiver.Upload
{
    // 最後に受け口へ送った宣言のパス集合。受け口は宣言の拡大を拒むので、次の走行の宣言はこの部分集合に留める
    // The path set of the declaration last sent to the receiver; the receiver refuses a grown declaration, so the next run's declaration stays a subset of it
    public static class PlaytestUploadDeclaredRecord
    {
        // 1行1パス。宣言規則が制御文字を拒むのでpathに改行は入らない
        // One path per line; the declaration rules refuse control characters, so a path never holds a newline
        public static void Write(string boxDirectory, IReadOnlyList<PlaytestDeclaredFile> files)
        {
            File.WriteAllLines(Path.Combine(boxDirectory, PlaytestOutboxScanner.DeclaredMarker), files.Select(file => file.Path));
        }

        // まだ一度も宣言していない箱ではnull（制限なし）
        // Null for a box never declared yet (no restriction)
        public static HashSet<string> Read(string boxDirectory)
        {
            var markerPath = Path.Combine(boxDirectory, PlaytestOutboxScanner.DeclaredMarker);
            if (!File.Exists(markerPath)) return null;
            return new HashSet<string>(File.ReadAllLines(markerPath).Where(line => line.Length != 0));
        }
    }
}
