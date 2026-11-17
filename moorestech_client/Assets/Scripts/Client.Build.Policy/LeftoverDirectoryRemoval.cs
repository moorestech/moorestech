using System;
using System.IO;

namespace Client.Build.Policy
{
    /// <summary>
    /// ディレクトリとその .meta を消す（無ければ何もしない）。失敗は理由を返し、報告は呼び元が行う
    /// Removes a directory and its .meta (does nothing when absent); a failure returns its reason and the caller reports it
    /// </summary>
    public static class LeftoverDirectoryRemoval
    {
        public static bool TryRemove(string directory, out bool removedAnything, out bool hadMeta, out string failureReason)
        {
            var meta = directory + ".meta";
            var hasDirectory = Directory.Exists(directory);
            hadMeta = File.Exists(meta);
            removedAnything = hasDirectory || hadMeta;
            failureReason = null;
            if (!removedAnything) return true;

            // 作業コピーのディスク IO は他プロセスのロック・権限で失敗しうる外部境界なので隔離し、理由は呼び元が LogError へ出す
            // Working-copy disk IO is an external boundary that can fail on locks or permissions, so isolate it; the caller logs the reason as an error
            try
            {
                if (hasDirectory) Directory.Delete(directory, true);
                if (hadMeta) File.Delete(meta);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                failureReason = e.Message;
                removedAnything = false;
                return false;
            }
            return true;
        }
    }
}
