using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Client.Game.InGame.Playtest.Progress;
using Game.Paths;

namespace Client.Starter.Playtest
{
    /// <summary>
    /// 前回セッションの印を読む処理を起動時の1箇所へ束ねる（ADR 0060 裁定5）。消費と書き手の設置がここで並ぶ。
    /// Bundles everything that reads the previous session's marks into one boot-time spot (ADR 0060 adjudication 5), where consumption and writer installation sit together.
    /// </summary>
    public static class PreviousSessionStartupTasks
    {
        public static void RunAtStartup(bool isRemoteConnection, string worldDirectory)
        {
            // 内蔵サーバーのスナップショットリングと録画リングが上書きを始める前に、前回セッションの記録を退避する
            // Salvage the previous session's records before the embedded snapshot ring and the recording ring start overwriting
            PreviousSessionSalvage.RunAtStartup(isRemoteConnection, WorldDataDirectory.FromWorldRoot(worldDirectory).SnapshotDirectory);

            // 正常終了マーカーの書き手を、消費と同じこの1箇所で据える。ロード中やゲート表示中の終了が異常終了に化ける窓を開けない
            // The clean-exit writer is installed at the same single spot that consumes the marks, leaving no window where a load-time or gate-time exit reads as a crash
            CleanExitMarkWriter.InstallAtStartup(RecordingProcessDirectories.CurrentProcessId());

            // 前回の書きかけの進行記録も、印を読む同じ1箇所で畳む。書く側（ProgressRecorder）は回収を知らない
            // The half-written progress records are folded at the same single spot that reads the marks; the writer (ProgressRecorder) knows nothing of the recovery
            ProgressSessionRecovery.RecoverLeftoverSessions(PreviousSessionSalvage.ArtifactsOrNotRunDefault().PreviousExitWasClean);
        }
    }
}
