using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.PlaytestReceiver.Http;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload
{
    // 最後に受け口へ送った宣言の世代とパス集合。受け口は同じ世代の食い違いと宣言の拡大を拒むので、次の宣言はこの部分集合に留め、縮めるなら世代を上げる
    // The generation and path set of the declaration last sent; the receiver refuses a same-generation mismatch or a grown set, so later declarations stay within it and bump the generation to shrink
    public sealed class PlaytestUploadDeclaredRecord
    {
        // 1行目の目印。宣言規則が制御文字を拒むので、TABを含む行がパスと取り違えられることはない
        // The first-line tag; the declaration rules refuse control characters, so a line holding a TAB is never mistaken for a path
        private const string GenerationLinePrefix = "generation\t";

        public readonly int Generation;
        public readonly HashSet<string> Paths;

        private PlaytestUploadDeclaredRecord(int generation, HashSet<string> paths)
        {
            Generation = generation;
            Paths = paths;
        }

        // 今回の宣言の世代を決めて記録し、その世代を返す。初回は1、前回と同じ集合なら同じ世代、変わった（縮んだ）なら1つ上げる
        // Settles, records and returns this declaration's generation: 1 at first, the same for an identical set, one higher for a changed (shrunk) set
        public static int Declare(string boxDirectory, IReadOnlyList<PlaytestDeclaredFile> files)
        {
            var earlier = Read(boxDirectory);
            var paths = new HashSet<string>(files.Select(file => file.Path));
            var generation = earlier == null ? 1 : earlier.Paths.SetEquals(paths) ? earlier.Generation : earlier.Generation + 1;
            var lines = new List<string> { GenerationLinePrefix + generation };
            lines.AddRange(files.Select(file => file.Path));
            File.WriteAllLines(Path.Combine(boxDirectory, PlaytestOutboxScanner.DeclaredMarker), lines);
            return generation;
        }

        // まだ一度も宣言していない箱ではnull（制限なし）。世代の行が無い旧形式は世代1として読む
        // Null for a box never declared yet (no restriction); the older form without a generation line reads as generation 1
        public static PlaytestUploadDeclaredRecord Read(string boxDirectory)
        {
            var markerPath = Path.Combine(boxDirectory, PlaytestOutboxScanner.DeclaredMarker);
            if (!File.Exists(markerPath)) return null;
            var lines = File.ReadAllLines(markerPath).Where(line => line.Length != 0).ToList();
            if (lines.Count == 0 || !lines[0].StartsWith(GenerationLinePrefix)) return new PlaytestUploadDeclaredRecord(1, new HashSet<string>(lines));

            // 世代が読めない記録は受け口と食い違うので、世代1として送り受け口の判定（409）に委ねる
            // An unreadable generation cannot match the receiver, so it is sent as generation 1 and left to the receiver's verdict (409)
            if (!int.TryParse(lines[0].Substring(GenerationLinePrefix.Length), out var generation) || generation < 1)
            {
                Debug.LogWarning($"[PlaytestReceiver] {Path.GetFileName(boxDirectory)} has an unreadable declared generation '{lines[0]}'; treating it as generation 1");
                generation = 1;
            }
            return new PlaytestUploadDeclaredRecord(generation, new HashSet<string>(lines.Skip(1)));
        }
    }
}
