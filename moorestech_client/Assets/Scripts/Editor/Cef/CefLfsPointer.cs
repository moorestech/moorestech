using System;
using System.IO;
using System.Text;

namespace Client.Editor
{
    /// <summary>
    /// Git LFS未解決のポインタファイルかを判定する（実体と殻の唯一の判定点）
    /// Decides whether a file is an unresolved Git LFS pointer; the single place that tells husk from payload
    /// </summary>
    public static class CefLfsPointer
    {
        // ポインタファイルは数百バイトなので、これを超える実体は読まずに除外する
        // A pointer file is a few hundred bytes, so anything larger is excluded without reading
        public const int MaximumPointerFileSize = 1024;

        private const string LfsPointerHeader = "version https://git-lfs";

        // Windows の MAX_PATH。これ以上の長さは拡張パス接頭辞が無いと Mono の FileStream が開けない
        // Windows MAX_PATH; Mono's FileStream cannot open longer paths without the extended-length prefix
        private const int WindowsMaxPath = 260;
        private const string WindowsExtendedPathPrefix = @"\\?\";
        private const string WindowsExtendedUncPrefix = @"\\?\UNC\";

        public static bool IsPointerFile(string filePath)
        {
            var openablePath = ToOpenablePath();
            if (!File.Exists(openablePath)) return false;
            if (MaximumPointerFileSize < new FileInfo(openablePath).Length) return false;

            // 小さいファイルの先頭だけ読んでヘッダを照合する
            // Read only the prefix of small files and match the header
            using var stream = File.OpenRead(openablePath);
            var prefixLength = (int)Math.Min(stream.Length, LfsPointerHeader.Length);
            var prefix = new byte[prefixLength];
            var bytesRead = stream.Read(prefix, 0, prefix.Length);

            return Encoding.ASCII.GetString(prefix, 0, bytesRead) == LfsPointerHeader;

            #region Internal

            // MAX_PATH超の絶対パスだけ拡張接頭辞を付ける
            // Prefix only over-MAX_PATH absolute paths on Windows with the extended-length prefix
            string ToOpenablePath()
            {
                if (Path.DirectorySeparatorChar != '\\') return filePath;

                var fullPath = Path.GetFullPath(filePath).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                if (fullPath.Length < WindowsMaxPath || fullPath.StartsWith(WindowsExtendedPathPrefix, StringComparison.Ordinal)) return fullPath;
                if (fullPath.StartsWith(@"\\", StringComparison.Ordinal)) return WindowsExtendedUncPrefix + fullPath.Substring(2);
                return WindowsExtendedPathPrefix + fullPath;
            }

            #endregion
        }
    }
}
