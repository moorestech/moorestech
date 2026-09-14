using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Game.Paths;
using UnityEditor;

namespace Client.Tests.EditModeInPlayingTest.Util
{
    // 開始ゲートは応答があるまで初期化を止めるため、応答者のいないテスト起動では先に印を置いて出さないようにする
    // The start gates hold initialization until answered, so a test boot with no one to answer pre-places the marks
    // 印は本番と同じ BugReports/ に置かれる。放置すると同意ゲートが二度と出ず本物のクラッシュの退避物を握り潰すため、自分が置いた分は必ず消す
    // The marks land in the production BugReports/; left behind they hide the consent gate forever and swallow a real crash's salvage
    [InitializeOnLoad]
    public static class PlaytestStartGateBypass
    {
        private const string ConsentFlagCreatedKey = "PlaytestStartGateBypass_ConsentFlagCreated";

        // 購読はドメインリロードで消えるため、Editorの読み込みごとに張り直す。SessionStateはリロードを越えて残る
        // The subscription dies with every domain reload, so it is re-established on each Editor load; SessionState outlives reloads
        static PlaytestStartGateBypass()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void Apply()
        {
            MarkCreated(ConsentFlagCreatedKey, !PlaytestConsentFlag.IsAcknowledged());
            PlaytestConsentFlag.Acknowledge();

            // 前回セッションとして数えられるpidを全て正常終了に倒す。1つでも異常終了が残ると確認ゲートが出て起動が止まる
            // Every pid that would count as a previous session is flipped to a clean exit; one unclean pid left would show the gate and stall the boot
            foreach (var processId in PreviousSessionProcessIds()) CleanExitMarker.MarkCleanExit(processId);
        }

        // 自分が置いた印だけを消す。元から在った印は開発者のものなので触らない
        // Removes only what this bypass created; a pre-existing mark belongs to the developer and is left alone
        public static void Restore()
        {
            DeleteIfCreated(ConsentFlagCreatedKey, PlaytestConsentFlag.FilePath);

            // 前回セッションぶんの印は起動時のConsumeで消えている。残るのはテスト中の終了パイプラインが書いた自pidの分だけ
            // The previous sessions' marks are gone by the boot-time consume; only this pid's mark, written by the test's shutdown pipeline, remains
            // PlayModeの停止は正常終了ではないので落とす
            // Stopping Play Mode is not a clean exit, so it is dropped
            var currentMarkerPath = CleanExitMarker.CleanMarkerPath(RecordingProcessDirectories.CurrentProcessId());
            if (File.Exists(currentMarkerPath)) File.Delete(currentMarkerPath);
        }

        private static IReadOnlyList<int> PreviousSessionProcessIds()
        {
            var currentProcessId = RecordingProcessDirectories.CurrentProcessId();
            var processIds = new List<int>(CleanExitMarker.SessionProcessIds());
            var takeover = RecordingProcessDirectories.TakeOverPreviousProcessDirectories(GameSystemPaths.BugReportRecordingDirectory, currentProcessId);
            foreach (var directory in takeover.Directories)
                if (!processIds.Contains(directory.ProcessId))
                    processIds.Add(directory.ProcessId);
            processIds.Remove(currentProcessId);
            return processIds;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) Restore();
        }

        private static void MarkCreated(string key, bool created)
        {
            if (created) SessionState.SetBool(key, true);
        }

        private static void DeleteIfCreated(string key, string path)
        {
            if (!SessionState.GetBool(key, false)) return;
            SessionState.SetBool(key, false);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
