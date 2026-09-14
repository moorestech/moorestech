using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 正常終了の意図が表明されたことだけを記録するマーカー。クラッシュは終了パイプラインに入れないので書かれない
    // A marker recording only that a graceful exit was intended; a crash never enters the shutdown pipeline, so it stays absent
    // 印はpidで割る。並列Editorが日常のこの環境で、片方の終了が他方のクラッシュ判定を書き換えないため
    // The marks are split by pid so that, with parallel Editors as the norm here, one exit never rewrites another's crash verdict
    public static class CleanExitMarker
    {
        public const string SessionMarkerPrefix = "session_";
        public const string CleanMarkerPrefix = "clean_";

        // Client.Game に InternalsVisibleTo が無く Client.Tests から internal が見えないため、公開面は public に留める
        // The surface stays public because Client.Game has no InternalsVisibleTo and Client.Tests cannot see internals
        public static string SessionMarkerPath(int processId)
        {
            return Path.Combine(GameSystemPaths.BugReportLastSessionDirectory, SessionMarkerPrefix + processId.ToString(CultureInfo.InvariantCulture));
        }

        public static string CleanMarkerPath(int processId)
        {
            return Path.Combine(GameSystemPaths.BugReportLastSessionDirectory, CleanMarkerPrefix + processId.ToString(CultureInfo.InvariantCulture));
        }

        // 生存印を置いたまま消えたpidが「異常終了したセッション」の正体。起動直後に1回だけ置く
        // A pid that vanished with its session mark still in place is exactly a crashed session; placed once, right at boot
        public static void MarkSessionStarted(int processId)
        {
            WriteMarker(SessionMarkerPath(processId), "セッション開始の印");
        }

        public static void MarkCleanExit(int processId)
        {
            WriteMarker(CleanMarkerPath(processId), "正常終了の印");
        }

        // 印が置かれている全pid。前回のプロセスを、録画の有無に依らず数え上げるための一覧
        // Every pid holding a session mark; enumerates the previous processes regardless of whether they recorded anything
        public static IReadOnlyList<int> SessionProcessIds()
        {
            var processIds = new List<int>();
            var directory = GameSystemPaths.BugReportLastSessionDirectory;
            if (!Directory.Exists(directory)) return processIds;

            // 印の一覧はディスク走査。権限や他プロセスのロックで失敗しても起動は続ける
            // Listing the marks is a disk scan; boot continues even if permissions or another process's lock make it fail
            try
            {
                foreach (var file in Directory.GetFiles(directory, SessionMarkerPrefix + "*"))
                    if (int.TryParse(Path.GetFileName(file).Substring(SessionMarkerPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var processId))
                        processIds.Add(processId);
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                Debug.LogWarning($"前回セッションの印を読めませんでした（前回分の判定ができません）: {e.Message}");
            }
            return processIds;
        }

        // 指定pidの印を読んで消す。読んだ時点で消すのは、同じpidを二度「前回」として数えないため
        // Reads then removes the pid's marks; removing on read keeps one pid from counting as "previous" twice
        public static bool ConsumeExitCleanFlag(int processId)
        {
            var cleanMarkerPath = CleanMarkerPath(processId);
            var wasClean = File.Exists(cleanMarkerPath);

            var cleanDeletion = SalvageFileOperations.DeleteFile(cleanMarkerPath);
            if (!cleanDeletion.Succeeded) Debug.LogWarning($"正常終了マーカーを消せませんでした pid:{processId}: {cleanDeletion.FailureReason}");

            var sessionDeletion = SalvageFileOperations.DeleteFile(SessionMarkerPath(processId));
            if (!sessionDeletion.Succeeded) Debug.LogWarning($"セッション開始の印を消せませんでした pid:{processId}: {sessionDeletion.FailureReason}");

            return wasClean;
        }

        // 印の書き出しも終了パイプラインの中で走る。ここで例外を上げるとセーブもApplication.Quitも到達しない
        // Writing a mark runs inside the shutdown pipeline; an exception escaping here would strand both the save and Application.Quit
        private static void WriteMarker(string path, string label)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                Debug.LogError($"{label}を書けませんでした（次回起動は異常終了として扱われます） {path}: {e.Message}");
            }
        }
    }
}
