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

        public static bool IsPointerFile(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            if (MaximumPointerFileSize < new FileInfo(filePath).Length) return false;

            // 小さいファイルの先頭だけ読んでヘッダを照合する
            // Read only the prefix of small files and match the header
            using var stream = File.OpenRead(filePath);
            var prefixLength = (int)Math.Min(stream.Length, LfsPointerHeader.Length);
            var prefix = new byte[prefixLength];
            var bytesRead = stream.Read(prefix, 0, prefix.Length);

            return Encoding.ASCII.GetString(prefix, 0, bytesRead) == LfsPointerHeader;
        }
    }
}
