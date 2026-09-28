using System;
using System.Diagnostics;
using System.IO;
using Client.RemoteExec.Run;
using Debug = UnityEngine.Debug;

namespace Client.RemoteExec.Access
{
    // 実行開始と結果を同じ連番で追記し、停止したコードも追跡できるようにする
    // Append starts and results with one sequence so stalled code remains traceable
    public static class RemoteExecLedger
    {
        public const string AttemptSignalFileName = "remote-exec-attempted";
        public const string FailureSignalFileName = "remote-exec-ledger-failed";
        private static readonly object AttemptSignalLock = new();
        private static string _attemptSignalPath;
        private static string _failureSignalPath;
        // PIDの取得はCurrentWriterの1回だけに畳む
        // Fold PID retrieval down to CurrentWriter's single call
        public static string CurrentFileName => CurrentWriter.FileName;
        public static string CurrentPath => CurrentWriter.Instance.FilePath;
        private static volatile bool _hasWriteFailure;
        private static volatile bool _hasAttempted;
        public static bool HasWriteFailure => _hasWriteFailure;
        public static bool HasAttempted => _hasAttempted;

        // 起動セッションの名前を開始時に受け、再生し直しでwriterを切り替える
        // Accept the boot session name and switch writers when Play Mode restarts
        public static void Initialize(string sessionName, string attemptSignalPath)
        {
            CurrentWriter.SetSessionName(sessionName);
            _attemptSignalPath = attemptSignalPath;
            _failureSignalPath = attemptSignalPath == null ? null : Path.Combine(Path.GetDirectoryName(attemptSignalPath), FailureSignalFileName);
        }

        public static bool IsLedgerFileName(string fileName)
        {
            return RemoteExecLedgerWriter.IsLedgerFileName(fileName);
        }

        public static string PathForFileName(string fileName)
        {
            return Path.Combine(RemoteExecAccessFile.DirectoryPath, fileName);
        }

        public static string PathFor(int processId, string sessionName)
        {
            return PathForFileName(RemoteExecLedgerWriter.FileNameFor(processId, sessionName));
        }

        internal static RemoteExecLedgerEntry AppendStart(RemoteExecTarget target, string body)
        {
            _hasAttempted = true;
            MarkAttempt();
            // 台帳のパス解決もディスク境界に含め、失敗しても送信コードの実行を続ける
            // Path resolution belongs to the disk boundary; submitted code still runs if it fails
            try
            {
                var writer = CurrentWriter.Instance;
                var sequence = writer.AppendStart(target, body, out var written);
                if (!written) RecordFailure();
                return new RemoteExecLedgerEntry(writer, sequence);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                RecordFailure();
                Debug.LogError($"[RemoteExec] 台帳のパスを解決できませんでした（実行記録が欠損）: {e.Message}");
                return new RemoteExecLedgerEntry(null, 0);
            }
        }

        internal static void AppendResult(RemoteExecLedgerEntry entry, RemoteExecOutcome outcome)
        {
            if (entry.Writer == null) return;
            if (!entry.Writer.AppendResult(entry.Sequence, outcome)) RecordFailure();
        }

        private static void MarkAttempt()
        {
            if (_attemptSignalPath == null) return;
            // 台帳とは別の印ディレクトリへ先に残し、クラッシュ後も書込失敗と未実行を区別する
            // Write first in the separate marks directory so a crash can distinguish failed writes from no run
            if (!TryWriteSignal(_attemptSignalPath)) RecordFailure();
        }

        private static void RecordFailure()
        {
            _hasWriteFailure = true;
            if (_failureSignalPath != null) TryWriteSignal(_failureSignalPath);
        }

        private static bool TryWriteSignal(string path)
        {
            // 印の永続化はディスクIO境界。印自身が書けない場合もログに明示する
            // Persisting a signal is a disk IO boundary; failure to write the signal is logged too
            try
            {
                lock (AttemptSignalLock)
                {
                    if (File.Exists(path)) return true;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, string.Empty);
                }
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                Debug.LogError($"[RemoteExec] 実行状態の印を書けませんでした（前回セッションの判定が欠損）: {e.Message}");
                return false;
            }
        }

        internal readonly struct RemoteExecLedgerEntry
        {
            internal readonly RemoteExecLedgerWriter Writer;
            internal readonly long Sequence;

            internal RemoteExecLedgerEntry(RemoteExecLedgerWriter writer, long sequence)
            {
                Writer = writer;
                Sequence = sequence;
            }
        }

        private static class CurrentWriter
        {
            private static readonly object Gate = new();
            private static string _sessionName = "session_" + System.DateTime.UtcNow.Ticks;
            private static RemoteExecLedgerWriter _instance;
            internal static string FileName => RemoteExecLedgerWriter.FileNameFor(Process.GetCurrentProcess().Id, _sessionName);

            internal static void SetSessionName(string sessionName)
            {
                lock (Gate)
                {
                    if (_sessionName == sessionName) return;
                    _sessionName = sessionName;
                    _instance = null;
                    _hasWriteFailure = false;
                    _hasAttempted = false;
                }
            }

            internal static RemoteExecLedgerWriter Instance
            {
                get
                {
                    lock (Gate)
                    {
                        if (_instance != null) return _instance;
                        _instance = new RemoteExecLedgerWriter(RemoteExecAccessFile.DirectoryPath,
                            Process.GetCurrentProcess().Id, _sessionName);
                        return _instance;
                    }
                }
            }
        }
    }
}
