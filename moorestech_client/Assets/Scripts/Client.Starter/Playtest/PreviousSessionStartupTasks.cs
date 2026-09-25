using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Client.Game.InGame.Playtest.Progress.Storage;
using UnityEngine;

namespace Client.Starter.Playtest
{
    /// <summary>
    /// 前回セッションの印を読む処理（退避と印の消費）を起動1回に1度、1箇所で行う（ADR 0060 裁定5・ADR 0065）。
    /// タイトルを通る起動（出展モードの自動開始を含む）はタイトルの照合通過で、タイトルを通らない直接起動（テスト・DSL・Editorの直接再生）はパイプラインで行う。
    /// 今回の終了印の書き手は、パイプラインの開始ゲート通過直後・最初のawait前に同期設置する。
    /// Reads the previous session's marks (salvage and consumption) once per boot at a single spot (ADR 0060 adjudication 5, ADR 0065).
    /// A boot through the title (the event-mode auto start included) does it when the launch check passes there; a direct boot that skips the title (tests, the DSL, an Editor direct play) does it in the pipeline.
    /// This boot's exit-mark writer is installed synchronously right after the pipeline's start gate, before the first await.
    /// </summary>
    public static class PreviousSessionStartupTasks
    {
        private static bool _salvagedThisBoot;

        // Editorの再生し直しは同じプロセスで起動をやり直すため、再生ごとに未退避へ戻す（前例: PlaytestLaunchProfile）
        // An Editor replay restarts the boot in the same process, so each play resets to "not salvaged" (precedent: PlaytestLaunchProfile)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayMode()
        {
            _salvagedThisBoot = false;
        }

        // タイトルの照合通過で1回だけ呼ぶ。退避元は前回セッション自身の所有印が決めるので、ここは今回の起動設定を渡さない（D-C3）
        // Called once when the launch check passes at the title; the previous session's own ownership mark decides the salvage source, so no setting of this boot is handed over (D-C3)
        internal static PreviousSessionArtifacts SalvageAtTitle()
        {
            // 退避はこの起動に1回（ADR 0060 裁定5）。直接起動の後にタイトルへ戻った再訪では、退避済みの資料をそのまま渡して未応答の確認を出し直す（D-C1）
            // The salvage happens once per boot (ADR 0060 adjudication 5); a revisit to the title after a direct boot hands the already-salvaged evidence over and asks the unanswered confirmation again (D-C1)
            if (_salvagedThisBoot)
            {
                Debug.Log("PreviousSessionStartupTasks: この起動の退避は済んでいるため、退避済みの資料をそのままタイトルの確認へ渡します");
                return PreviousSessionSalvage.RequireArtifacts();
            }

            // 退避の前にこの起動のセッション名を確定する。退避は「今回以外」を畳むため（F05）
            // This boot's session name is fixed before salvaging, because the salvage folds everything but this one (F05)
            ProcessSessionScope.BeginNewSession();
            return Salvage();
        }

        // パイプラインの開始ゲート直後・最初のawait前に呼ぶ。ホスト未起動の待機中の停止も記録するため
        // Called right after the pipeline's start gate and before the first await, so stops while waiting for the host are recorded too
        public static void BeginCurrentSessionMarks()
        {
            // 試行ごとに新しい段へ書き始める（F05）。同じプロセスでの再試行が、失敗した試行の印を上書きして証拠を消さないため
            // Every attempt starts writing in a fresh level (F05), so a retry in the same process cannot overwrite the failed attempt's marks
            ProcessSessionScope.BeginNewSession();
            CleanExitMarkWriter.InstallAtStartup(RecordingProcessDirectories.CurrentProcessId(), ProcessSessionScope.CurrentSessionName);
        }

        // BeginCurrentSessionMarksの後に呼ぶ。タイトルで退避済みなら進行記録の回収だけを行う
        // Called after BeginCurrentSessionMarks; when the title already salvaged, it only recovers the progress records
        public static void RunAtStartup(bool collectsPlaytestRecords)
        {
            if (!_salvagedThisBoot)
            {
                // Editorの迂回印は読んだ時点で消費される。ここで読まないと次の手動のタイトル起動へ持ち越され、確認が1回消える
                // The Editor bypass mark is consumed on read; leaving it unread would carry it to the next manual title boot and skip its confirmations once
                var unattendedReason = PlaytestStartGateBypass.UnattendedReason();
                Debug.Log($"PreviousSessionStartupTasks: タイトルを経由しない起動のため、ここで前回セッションを退避します。同意と前回異常終了の確認はこの起動では出さず、未応答の印は次にタイトルを通る起動で聞き直します unattended:{unattendedReason ?? "none"}");
                Salvage();
            }

            // 終了印と収集同意は独立。収集しない起動は前回の進行記録を次の収集起動に残す（理由はPlaytestRecordCollectionがログ済み）
            // Exit marks are independent of collection consent; a non-collecting boot leaves leftover progress for the next collecting boot (PlaytestRecordCollection logged why)
            if (!collectsPlaytestRecords) return;

            // 前回の書きかけの進行記録を、消費した印の結果で畳む（F19）
            // Folds the half-written progress records by the consumed marks' outcome (F19)
            ProgressSessionRecovery.RecoverLeftoverSessions(PreviousSessionSalvage.RequireArtifacts().ExitedCleanlyByProcessId);
        }

        // 呼ぶ前にこの起動のセッション名を確定しておくこと。退避は「今回以外」を畳む（F05）
        // This boot's session name must be fixed before calling, because the salvage folds everything but this one (F05)
        private static PreviousSessionArtifacts Salvage()
        {
            var artifacts = PreviousSessionSalvage.RunAtStartup();
            _salvagedThisBoot = true;
            return artifacts;
        }
    }
}
