using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Client.Game.InGame.BugReport.DiskOperations;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // セッションの開始・終了の意思・書き出し完了を記録する印。クラッシュは終了パイプラインに入れないので終了側の印は書かれない
    // Marks recording a session's start, its declared exit intent and its finished flush; a crash never enters the shutdown pipeline, so the exit-side marks stay absent
    // 印は marks/pid_<PID>/session_<utcTicks>/ で割る。並列Editorの終了も、同じpidでの再生し直しも、他セッションの判定を書き換えない（F05）
    // The marks are split by marks/pid_<PID>/session_<utcTicks>/, so neither a parallel Editor's exit nor a same-pid replay rewrites another session's verdict (F05)
    public static class CleanExitMarker
    {
        public const string MarksDirectoryName = "marks";
        public const string StartedFileName = "started";
        public const string ExitIntentFileName = "exit_intent";
        public const string CleanFileName = "clean";
        public const string OriginFileName = "origin.json";

        private static string MarksRoot => Path.Combine(GameSystemPaths.BugReportLastSessionDirectory, MarksDirectoryName);

        private static string SessionMarkDirectory(int processId, string sessionName)
        {
            return ProcessSessionScope.SessionDirectoryFor(MarksRoot, processId, sessionName);
        }

        private static string StartedMarkerPath(int processId, string sessionName)
        {
            return Path.Combine(SessionMarkDirectory(processId, sessionName), StartedFileName);
        }

        private static string ExitIntentMarkerPath(int processId, string sessionName)
        {
            return Path.Combine(SessionMarkDirectory(processId, sessionName), ExitIntentFileName);
        }

        private static string CleanMarkerPath(int processId, string sessionName)
        {
            return Path.Combine(SessionMarkDirectory(processId, sessionName), CleanFileName);
        }

        // 開始の印を置いたまま消えたセッションが「異常終了したセッション」の正体。出所も同時に書き、前回クラッシュの箱に使う（F12）
        // A session that vanished with its start mark in place is exactly a crashed one; the origin is written alongside for the previous-crash box (F12)
        public static void MarkSessionStarted(int processId, string sessionName, SessionOriginSnapshot origin)
        {
            WriteMarker(StartedMarkerPath(processId, sessionName), "セッション開始の印");
            origin.WriteTo(Path.Combine(SessionMarkDirectory(processId, sessionName), OriginFileName));
        }

        public static void MarkExitIntent(int processId, string sessionName)
        {
            WriteMarker(ExitIntentMarkerPath(processId, sessionName), "終了の意思表明の印");
        }

        public static void MarkCleanExit(int processId, string sessionName)
        {
            WriteMarker(CleanMarkerPath(processId, sessionName), "正常終了の印");
        }

        // 印が置かれている全セッション。前回のプロセスを、録画の有無に依らず数え上げるための一覧
        // Every session holding marks; enumerates the previous sessions regardless of whether they recorded anything
        public static IReadOnlyList<MarkedSession> MarkedSessions()
        {
            var sessions = new List<MarkedSession>();
            if (!Directory.Exists(MarksRoot)) return sessions;

            // 印の一覧はディスク走査。権限や他プロセスのロックで失敗しても起動は続ける
            // Listing the marks is a disk scan; boot continues even if permissions or another process's lock make it fail
            try
            {
                foreach (var processDirectory in Directory.GetDirectories(MarksRoot)) CollectSessions(processDirectory);
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                Debug.LogWarning($"前回セッションの印を読めませんでした（前回分の判定ができません）: {e.Message}");
            }
            return sessions;

            #region Internal

            void CollectSessions(string processDirectory)
            {
                if (!RecordingProcessDirectories.TryParseProcessId(Path.GetFileName(processDirectory), out var processId))
                {
                    Debug.LogWarning($"pid_<PID> の綴りでない印のディレクトリは数えません: {processDirectory}");
                    return;
                }
                foreach (var sessionDirectory in Directory.GetDirectories(processDirectory))
                {
                    var sessionName = Path.GetFileName(sessionDirectory);
                    if (!ProcessSessionScope.IsSessionDirectoryName(sessionName))
                    {
                        Debug.LogWarning($"session_<utcTicks> の綴りでない印のディレクトリは数えません: {sessionDirectory}");
                        continue;
                    }

                    // 開始の印が無い段は開始していないセッション。終了側の印だけで数えると、開始していない物を異常終了と読む
                    // A level without the start mark never began; counting it from exit-side marks alone would read a session that never started as a crash
                    if (!File.Exists(Path.Combine(sessionDirectory, StartedFileName)))
                    {
                        Debug.LogWarning($"開始の印が無いため前回セッションとして数えません: {sessionDirectory}");
                        continue;
                    }
                    sessions.Add(new MarkedSession { ProcessId = processId, SessionName = sessionName });
                }
            }

            #endregion
        }

        // 指定セッションの印を読んで消す。読んだ時点で消すのは、同じセッションを二度「前回」として数えないため
        // Reads then removes the session's marks; removing on read keeps one session from counting as "previous" twice
        public static SessionExitRecord ConsumeSessionMarks(int processId, string sessionName)
        {
            var record = new SessionExitRecord
            {
                ExitedCleanly = File.Exists(CleanMarkerPath(processId, sessionName)),
                Origin = SessionOriginSnapshot.ReadFrom(Path.Combine(SessionMarkDirectory(processId, sessionName), OriginFileName), out var originFailure),
            };
            record.OriginMissingReason = originFailure;
            record.ShutdownStalled = !record.ExitedCleanly && File.Exists(ExitIntentMarkerPath(processId, sessionName));
            if (record.ShutdownStalled) Debug.LogWarning($"pid {processId} {sessionName} は終了の意思表明の後、書き出し完了の前に止まりました（終了処理中の停止として異常終了に数えます）");

            var directory = SessionMarkDirectory(processId, sessionName);
            var deletion = BugReportDiskOperations.DeleteDirectory(directory);
            if (!deletion.Succeeded) Debug.LogWarning($"セッションの印を消せませんでした pid:{processId} {sessionName}: {deletion.FailureReason}");

            var parentDeletion = BugReportDiskOperations.DeleteDirectoryIfEmpty(Path.GetDirectoryName(directory));
            if (!parentDeletion.Succeeded) Debug.LogWarning($"空になったpidの印ディレクトリを消せませんでした pid:{processId}: {parentDeletion.FailureReason}");
            return record;
        }

        // 印の書き出しも終了パイプラインの中で走る。ここで例外を上げるとセーブもApplication.Quitも到達しない
        // Writing a mark runs inside the shutdown pipeline; an exception escaping here would strand both the save and Application.Quit
        private static void WriteMarker(string path, string label)
        {
            // 起動直後・終了直前の印書き込みはディスクIO。権限や他Editorのロックで失敗しても起動/終了は続ける
            // Writing the mark at boot or shutdown is disk IO; boot and shutdown continue even if permissions or another Editor's lock make it fail
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, DateTime.UtcNow.ToString(BugReportBundleLayout.Utc8601Format, CultureInfo.InvariantCulture));
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                Debug.LogError($"{label}を書けませんでした（次回起動は異常終了として扱われます） {path}: {e.Message}");
            }
        }
    }
}
