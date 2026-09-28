using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.DiskOperations;
using Client.RemoteExec.Access;
using Game.Paths;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Client.Game.InGame.BugReport.LastSession
{
    internal sealed class PreviousSessionRemoteExecLedger
    {
        internal string Name;
        internal bool Attempted;
        internal bool WriteFailed;
    }

    // 全クラッシュセッションの台帳名を未応答の起動を跨いで保持する
    // Keep every crashed session's ledger name across unanswered launches
    internal static class PreviousSessionRemoteExecLedgers
    {
        private const string FileName = "previous-remote-exec-ledgers.json";

        internal static string PathIn(string lastSessionDirectory)
        {
            return Path.Combine(lastSessionDirectory, FileName);
        }

        internal static void Save(string lastSessionDirectory, IReadOnlyList<PreviousProcessSession> sessions, bool hadPendingReport, SalvageMissingLog missing)
        {
            var entries = new Dictionary<string, PreviousSessionRemoteExecLedger>();
            // 未応答の前世代があれば台帳を引き継ぐ。新しいクラッシュで一覧を上書きすると元台帳が孤立する
            // Carry unanswered ledgers forward; replacing the index on a new crash would orphan their sources
            if (hadPendingReport && TryRead(lastSessionDirectory, out var previous, out var previousReason))
            {
                if (previousReason != null) missing.Report(BugReportBundleLayout.RemoteExecDirectoryName, $"前世代の遠隔実行台帳一覧を引き継げなかった: {previousReason}");
                else foreach (var entry in previous) entries[entry.Name] = entry;
            }
            foreach (var session in sessions)
            {
                var name = session.Origin?.RemoteExecLedgerFileName;
                if (name != null) entries[name] = new PreviousSessionRemoteExecLedger
                {
                    Name = name,
                    Attempted = session.Origin.RemoteExecAttempted,
                    WriteFailed = session.Origin.RemoteExecLedgerWriteFailed,
                };
                if (session.Origin == null)
                    missing.Report(BugReportBundleLayout.RemoteExecDirectoryName, $"pid {session.ProcessId} {session.SessionName} の出所が読めず遠隔実行の有効状態が不明");
            }

            // 空の一覧も書き、次回の未応答起動に今回の状態を明示する
            // Write an empty index too, explicitly recording this generation for an unanswered launch
            var path = PathIn(lastSessionDirectory);
            var ordered = new List<string>(entries.Keys);
            ordered.Sort(StringComparer.Ordinal);
            var files = new JArray();
            foreach (var name in ordered) files.Add(new JObject
            {
                ["name"] = name,
                ["attempted"] = entries[name].Attempted,
                ["writeFailed"] = entries[name].WriteFailed,
            });
            var json = new JObject { ["ledgers"] = files };
            var write = BugReportFileOperations.WriteText(path + ".tmp", json.ToString(Formatting.Indented));
            if (!write.Succeeded)
            {
                missing.Report(BugReportBundleLayout.RemoteExecDirectoryName, write.FailureReason);
                return;
            }
            try
            {
                if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
                else File.Move(path + ".tmp", path);
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                missing.Report(BugReportBundleLayout.RemoteExecDirectoryName, $"遠隔実行台帳一覧を書けなかった: {e.Message}");
            }
        }

        internal static bool TryRead(string lastSessionDirectory, out List<PreviousSessionRemoteExecLedger> entries, out string reason)
        {
            entries = new List<PreviousSessionRemoteExecLedger>();
            reason = null;
            var path = PathIn(lastSessionDirectory);
            if (!File.Exists(path)) return false;
            var read = BugReportFileOperations.ReadText(path, out var text);
            if (!read.Succeeded)
            {
                reason = read.FailureReason;
                return true;
            }
            JArray files;
            // 台帳一覧は前回プロセスの外部JSON。壊れた入力を箱の欠損へ隔離する
            // The index is external JSON from a previous process; isolate corrupt input as a bundle gap
            try
            {
                var obj = JObject.Parse(text);
                files = obj["ledgers"] as JArray;
            }
            catch (JsonException e)
            {
                reason = $"遠隔実行台帳一覧を読めなかった: {e.Message}";
                return true;
            }
            if (files == null)
            {
                reason = "遠隔実行台帳一覧にledgersが無い";
                return true;
            }
            foreach (var token in files)
            {
                var entry = token as JObject;
                var name = entry?["name"]?.Type == JTokenType.String ? (string)entry["name"] : null;
                if (name == null || !RemoteExecLedger.IsLedgerFileName(name) || entry["attempted"]?.Type != JTokenType.Boolean || entry["writeFailed"]?.Type != JTokenType.Boolean)
                {
                    entries.Clear();
                    reason = $"遠隔実行台帳一覧の項目が不正: {name}";
                    return true;
                }
                entries.Add(new PreviousSessionRemoteExecLedger
                {
                    Name = name,
                    Attempted = (bool)entry["attempted"],
                    WriteFailed = (bool)entry["writeFailed"],
                });
            }
            return true;
        }

        internal static void Clear(string lastSessionDirectory, SalvageMissingLog missing)
        {
            var deletion = BugReportFileOperations.DeleteFile(PathIn(lastSessionDirectory));
            if (!deletion.Succeeded) missing.Report(BugReportBundleLayout.RemoteExecDirectoryName, deletion.FailureReason);
        }
    }
}
