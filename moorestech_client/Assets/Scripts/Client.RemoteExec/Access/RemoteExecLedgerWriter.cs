using System;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using Client.RemoteExec.Run;
using Game.Paths;
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
        private readonly string _sessionName;
        private readonly object _writeLock = new();
        private long _sequence;

        // 台帳ファイル名の接頭辞・拡張子の正本。名前の形式を変えるときはここだけ直す
        // The single source of truth for the ledger file name's prefix and extension; change the format here alone
        internal const string FileNamePrefix = "ledger-";
        internal const string FileNameExtension = ".jsonl";
        internal static readonly string FileNamePattern = $"^{Regex.Escape(FileNamePrefix)}[0-9]+-{Regex.Escape(ProcessSessionName.Prefix)}{ProcessSessionName.NumericSuffixPattern}{Regex.Escape(FileNameExtension)}$";
        private static readonly Regex NameMatcher = new(FileNamePattern, RegexOptions.Compiled);

        internal static bool IsLedgerFileName(string fileName)
        {
            return fileName != null && NameMatcher.IsMatch(fileName);
        }

        internal string FilePath => Path.Combine(_directory, FileNameFor(_processId, _sessionName));

        internal RemoteExecLedgerWriter(string directory, int processId, string sessionName)
        {
            _directory = directory;
            _processId = processId;
            _sessionName = sessionName;
        }

        internal static string FileNameFor(int processId, string sessionName)
        {
            return $"{FileNamePrefix}{processId}-{sessionName}{FileNameExtension}";
        }

        internal long AppendStart(RemoteExecTarget target, string body, out bool written)
        {
            var sequence = Interlocked.Increment(ref _sequence);
            var line = new JObject { ["event"] = "start", ["sequence"] = sequence, ["at"] = DateTime.UtcNow.ToString("o"), ["target"] = RemoteExecTargetWireName.ToWireName(target), ["code"] = body };
            written = Append(line);
            return sequence;
        }

        internal bool AppendResult(long sequence, RemoteExecOutcome outcome)
        {
            // outcomeの綴りはHTTP応答と同じ変換器（RemoteExecOutcomeのStringEnumConverter）に通す
            // The outcome's spelling goes through the same converter as the HTTP response (RemoteExecOutcome's StringEnumConverter)
            var line = new JObject { ["event"] = "result", ["sequence"] = sequence, ["at"] = DateTime.UtcNow.ToString("o"), ["outcome"] = JToken.FromObject(outcome) };
            return Append(line);
        }

        private bool Append(JObject entry)
        {
            // ディスクIO境界で保存先を用意し、失敗時は欠損をログに残す
            // Create the destination at the disk IO boundary and log missing entries on failure
            try
            {
                lock (_writeLock)
                {
                    RemoteExecDirectory.EnsureCreated(_directory);
                    File.AppendAllText(FilePath, entry.ToString(Formatting.None) + "\n");
                }
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[RemoteExec] 台帳に書けませんでした（実行記録が欠損）: {e.Message}");
                return false;
            }
        }
    }
}
