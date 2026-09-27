using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Debug = UnityEngine.Debug;

namespace Client.RemoteExec.Access
{
    public static class RemoteExecLedger
    {
        private static readonly object WriteLock = new();

        public static string PathFor(int processId)
        {
            return Path.Combine(RemoteExecAccessFile.DirectoryPath, $"ledger-{processId}.jsonl");
        }

        public static void Append(string target, string body, bool ok)
        {
            var line = new JObject { ["at"] = DateTime.UtcNow.ToString("o"), ["target"] = target, ["ok"] = ok, ["code"] = body }.ToString(Formatting.None);

            // ディスクIO失敗でも結果を返し、台帳の欠損をログに残す
            // Return the result on disk IO failure and log the missing ledger entry
            try
            {
                lock (WriteLock) File.AppendAllText(PathFor(Process.GetCurrentProcess().Id), line + "\n");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[RemoteExec] 台帳に書けませんでした（実行記録が欠損）: {e.Message}");
            }
        }
    }
}
