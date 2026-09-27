using System.Diagnostics;
using System.IO;

namespace Client.RemoteExec.Access
{
    // 実行開始と結果を同じ連番で追記し、停止したコードも追跡できるようにする
    // Append starts and results with one sequence so stalled code remains traceable
    public static class RemoteExecLedger
    {
        public static string CurrentFileName => RemoteExecLedgerWriter.FileNameFor(Process.GetCurrentProcess().Id);

        public static string PathFor(int processId)
        {
            return Path.Combine(RemoteExecAccessFile.DirectoryPath, RemoteExecLedgerWriter.FileNameFor(processId));
        }

        public static long AppendStart(string target, string body)
        {
            return CurrentWriter.Instance.AppendStart(target, body);
        }

        public static void AppendResult(long sequence, bool ok)
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
