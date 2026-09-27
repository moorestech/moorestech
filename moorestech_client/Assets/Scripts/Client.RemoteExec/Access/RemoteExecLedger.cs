using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Client.RemoteExec.Run;

namespace Client.RemoteExec.Access
{
    // 実行開始と結果を同じ連番で追記し、停止したコードも追跡できるようにする
    // Append starts and results with one sequence so stalled code remains traceable
    public static class RemoteExecLedger
    {
        // 台帳ファイル名の判定はWriterの接頭辞・拡張子定数から組み立て、書く側と別の文字列にしない
        // Build the file-name pattern from the writer's prefix/extension constants, never a separate literal
        private static readonly Regex LedgerFileNamePattern = new(
            $"^{Regex.Escape(RemoteExecLedgerWriter.FileNamePrefix)}[0-9]+{Regex.Escape(RemoteExecLedgerWriter.FileNameExtension)}$",
            RegexOptions.Compiled);

        // PIDの取得はCurrentWriterの1回だけに畳む
        // Fold PID retrieval down to CurrentWriter's single call
        public static string CurrentFileName => Path.GetFileName(CurrentWriter.Instance.FilePath);
        public static string CurrentPath => CurrentWriter.Instance.FilePath;

        public static bool IsLedgerFileName(string fileName)
        {
            return fileName != null && LedgerFileNamePattern.IsMatch(fileName);
        }

        public static string PathForFileName(string fileName)
        {
            return Path.Combine(RemoteExecAccessFile.DirectoryPath, fileName);
        }

        public static string PathFor(int processId)
        {
            return PathForFileName(RemoteExecLedgerWriter.FileNameFor(processId));
        }

        internal static long AppendStart(RemoteExecTarget target, string body)
        {
            return CurrentWriter.Instance.AppendStart(target, body);
        }

        internal static void AppendResult(long sequence, bool ok)
        {
            CurrentWriter.Instance.AppendResult(sequence, ok);
        }

        private static class CurrentWriter
        {
            internal static readonly RemoteExecLedgerWriter Instance =
                new(RemoteExecAccessFile.DirectoryPath, Process.GetCurrentProcess().Id);
        }
    }
}
