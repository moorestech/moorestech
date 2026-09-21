using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Client.Game.InGame.Playtest.Progress.Storage;

namespace Client.Starter.Playtest
{
    /// <summary>
    /// 終了印を同期設置し、前回資料を後で退避する。
    /// Installs exit marks now and salvages prior evidence later.
    /// </summary>
    public static class PreviousSessionStartupTasks
    {
        public static void BeginCurrentSessionMarks()
        {
            // 最初のawait前に識別と書き手を揃え、ホスト未起動でも停止を記録する
            // Establish identity and writer before the first await so stops are recorded even without a host
            ProcessSessionScope.BeginNewSession();
            CleanExitMarkWriter.InstallAtStartup(RecordingProcessDirectories.CurrentProcessId(), ProcessSessionScope.CurrentSessionName);
        }

        public static void RunAtStartup(bool collectsPlaytestRecords)
        {
            // 内蔵サーバーのスナップショットリングと録画リングが上書きを始める前に、前回セッションの記録を退避する
            // Salvage the previous session's records before the embedded snapshot ring and the recording ring start overwriting
            var artifacts = PreviousSessionSalvage.RunAtStartup();

            // 終了印と収集同意は独立。収集しない起動は前回の進行記録を次の収集起動に残す（理由はPlaytestRecordCollectionがログ済み）
            // Exit marks are independent of collection consent; a non-collecting boot leaves leftover progress for the next collecting boot (PlaytestRecordCollection logged why)
            if (!collectsPlaytestRecords) return;

            // 前回の書きかけの進行記録も、印を読む同じ1箇所で畳む。終わり方は残骸のpidごとに、消費した印の結果で決める（F19）
            // The half-written progress records are folded at the same single spot; each leftover's ending is decided per pid from the consumed marks (F19)
            ProgressSessionRecovery.RecoverLeftoverSessions(artifacts.ExitedCleanlyByProcessId);
        }
    }
}
