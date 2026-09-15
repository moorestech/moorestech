using System.Collections.Generic;
using System.IO;

namespace Game.Paths
{
    /// <summary>「接頭辞+tick+拡張子」形のファイル名を解析・列挙する。規則そのもの（接頭辞・拡張子）はWorldDataDirectoryが持つ</summary>
    /// <summary>Parses and lists "prefix + tick + extension" file names; the rule itself (prefix and extension) stays in WorldDataDirectory</summary>
    internal static class TickSuffixedFileNames
    {
        // tick昇順で返す。辞書順で並べると桁を跨いだ瞬間に最古が最新になる
        // Returned in tick order; lexicographic order makes the oldest look newest once the digits grow
        public static IReadOnlyList<string> EnumerateByTick(string directory, string prefix, string extension)
        {
            var ticks = new List<ulong>();
            if (directory == null || !Directory.Exists(directory)) return new List<string>();
            foreach (var path in Directory.GetFiles(directory, prefix + "*" + extension))
            {
                if (TryParseTick(Path.GetFileName(path), prefix, extension, out var tick)) ticks.Add(tick);
            }
            ticks.Sort();

            var result = new List<string>(ticks.Count);
            foreach (var tick in ticks) result.Add(Path.Combine(directory, $"{prefix}{tick}{extension}"));
            return result;
        }

        public static bool TryParseTick(string fileName, string prefix, string extension, out ulong tick)
        {
            tick = 0;
            if (!fileName.StartsWith(prefix) || !fileName.EndsWith(extension)) return false;
            var core = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - extension.Length);
            return ulong.TryParse(core, out tick);
        }
    }
}
