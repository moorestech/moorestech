using System.IO;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
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
        private const string CleanExitMarkCreatedKey = "PlaytestStartGateBypass_CleanExitMarkCreated";

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

            MarkCreated(CleanExitMarkCreatedKey, !File.Exists(CleanExitMarker.FilePath));
            CleanExitMarker.MarkCleanExit();
        }

        // 自分が置いた印だけを消す。元から在った印は開発者のものなので触らない
        // Removes only what this bypass created; a pre-existing mark belongs to the developer and is left alone
        public static void Restore()
        {
            DeleteIfCreated(ConsentFlagCreatedKey, PlaytestConsentFlag.FilePath);

            // 正常終了マーカーは起動時のConsumeで消えるが、テスト中の終了パイプラインが書き直した分も落とす（PlayModeの停止は正常終了ではない）
            // The clean-exit marker is consumed at boot, but any rewrite by the test's shutdown pipeline is dropped too: stopping Play Mode is not a clean exit
            DeleteIfCreated(CleanExitMarkCreatedKey, CleanExitMarker.FilePath);
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
