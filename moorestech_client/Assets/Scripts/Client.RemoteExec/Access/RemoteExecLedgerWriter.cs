using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.RemoteExec.Access
{
    // 指定ディレクトリへ開始・結果を追記し、テストと製品で同じ書込処理を使う
    // Append starts and results to a chosen directory with the same writer in tests and production
    internal sealed class RemoteExecLedgerWriter
    {
        private readonly string _directory;
        private readonly int _processId;
        private readonly object _writeLock = new();
        private long _sequence;

        internal string FilePath => Path.Combine(_directory, FileNameFor(_processId));

        internal RemoteExecLedgerWriter(string directory, int processId)
        {
            _directory = directory;
            _processId = processId;
        }

        internal static string FileNameFor(int processId)
        {
            return $"ledger-{processId}.jsonl";
        }

        internal long AppendStart(string target, string body)
        {
            var sequence = Interlocked.Increment(ref _sequence);
            var line = new JObject { ["event"] = "start", ["sequence"] = sequence, ["at"] = DateTime.UtcNow.ToString("o"), ["target"] = target, ["code"] = body };
            Append(line);
            return sequence;
        }

        internal void AppendResult(long sequence, bool ok)
        {
            var line = new JObject { ["event"] = "result", ["sequence"] = sequence, ["at"] = DateTime.UtcNow.ToString("o"), ["ok"] = ok };
            Append(line);
        }

        private void Append(JObject entry)
        {
            // ディスクIO境界で保存先を用意し、失敗時は欠損をログに残す
            // Create the destination at the disk IO boundary and log missing entries on failure
            try
            {
                lock (_writeLock)
                {
                    Directory.CreateDirectory(_directory);
                    File.AppendAllText(FilePath, entry.ToString(Formatting.None) + "\n");
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[RemoteExec] 台帳に書けませんでした（実行記録が欠損）: {e.Message}");
            }
        }
    }
}
