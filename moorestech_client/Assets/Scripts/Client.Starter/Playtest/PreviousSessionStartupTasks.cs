using System.Collections.Generic;
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
        public static void RunAtStartup(bool collectsPlaytestRecords, bool isRemoteConnection, string worldDirectory)
        {
            // この起動のセッション名を最初に確定する。退避は「今回以外」を畳み、書き手は全員この名前の下へ書く（F05）
            // This boot's session name is fixed first: the salvage folds everything else, and every writer writes under this name (F05)
            ProcessSessionScope.BeginNewSession();

            // 内蔵サーバーのスナップショットリングと録画リングが上書きを始める前に、前回セッションの記録を退避する
            // Salvage the previous session's records before the embedded snapshot ring and the recording ring start overwriting
            var artifacts = PreviousSessionSalvage.RunAtStartup(isRemoteConnection, WorldDataDirectory.FromWorldRoot(worldDirectory).SnapshotDirectory);

            // 記録を集めない起動は今回の印を書かず、前回の進行記録も回収しない。回収は次に集める起動が行う（理由はPlaytestRecordCollectionがログ済み）
            // A boot that collects nothing writes no marks of its own and leaves the leftover progress records to the next collecting boot (PlaytestRecordCollection logged why)
            if (!collectsPlaytestRecords) return;

            // 正常終了マーカーの書き手を、消費と同じこの1箇所で据える。ロード中やゲート表示中の終了が異常終了に化ける窓を開けない
            // The clean-exit writer is installed at the same single spot that consumes the marks, leaving no window where a load-time or gate-time exit reads as a crash
            CleanExitMarkWriter.InstallAtStartup(RecordingProcessDirectories.CurrentProcessId(), ProcessSessionScope.CurrentSessionName);

            // 前回の書きかけの進行記録も、印を読む同じ1箇所で畳む。pidごとの判定へ移すまでは、全pidが正常終了だったかへ畳んで渡す（F19の暫定）
            // The half-written progress records are folded at the same single spot; until the per-pid verdict lands they receive "did every pid exit cleanly" (interim for F19)
            ProgressSessionRecovery.RecoverLeftoverSessions(AllExitedCleanly(artifacts.ExitedCleanlyByProcessId));
        }

        private static bool AllExitedCleanly(IReadOnlyDictionary<int, bool> exitedCleanlyByProcessId)
        {
            foreach (var exitedCleanly in exitedCleanlyByProcessId.Values)
                if (!exitedCleanly) return false;
            return true;
        }
    }
}
