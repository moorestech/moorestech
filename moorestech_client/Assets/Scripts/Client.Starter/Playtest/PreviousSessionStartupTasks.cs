using System;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Client.Game.InGame.Playtest.Progress.Storage;
using Game.Paths;
using UnityEngine;

namespace Client.Starter.Playtest
{
    /// <summary>
    /// 前回セッションの印を読む処理（退避と印の消費）を起動1回に1度、1箇所で行う（ADR 0060 裁定5・ADR 0065）。
    /// タイトルを通る起動はタイトルの照合通過で、タイトルを通らない直接起動（テスト・DSL・出展モードの自動開始）はパイプライン先頭で行う。
    /// 正常終了の書き手と進行記録の回収は、識別（検証済みSteamID）が確定した後のパイプライン先頭に置く。
    /// Reads the previous session's marks (salvage and consumption) once per boot at a single spot (ADR 0060 adjudication 5, ADR 0065).
    /// A boot through the title does it when the launch check passes there; a direct boot that skips the title (tests, the DSL, event-mode auto start) does it at the head of the pipeline.
    /// The clean-exit writer and the progress recovery sit at the head of the pipeline, after the identity (verified SteamID) is settled.
    /// </summary>
    public static class PreviousSessionStartupTasks
    {
        private static bool _salvagedThisBoot;

        // Editorの再生し直しは同じプロセスで起動をやり直すため、再生ごとに未退避へ戻す（前例: PlaytestLaunchGate）
        // An Editor replay restarts the boot in the same process, so each play resets to "not salvaged" (precedent: PlaytestLaunchGate)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayMode()
        {
            _salvagedThisBoot = false;
        }

        // タイトルの照合通過で1回だけ呼ぶ。退避元は前回セッション自身の印が決めるので、ここは今回の起動設定を渡さない（D-C3）
        // Called once when the launch check passes at the title; the previous session's own mark decides the salvage source, so no setting of this boot is handed over (D-C3)
        internal static PreviousSessionArtifacts SalvageAtTitle()
        {
            if (_salvagedThisBoot) throw new InvalidOperationException("PreviousSessionStartupTasks: この起動の退避は済んでいます（タイトルの退避が2回目に到達しました）");

            // 退避の前にこの起動のセッション名を確定する。退避は「今回以外」を畳むため（F05）
            // This boot's session name is fixed before salvaging, because the salvage folds everything but this one (F05)
            ProcessSessionScope.BeginNewSession();
            return Salvage();
        }

        // パイプライン先頭で呼ぶ。タイトルで退避済みなら書き手の設置と回収だけを行う
        // Called at the head of the pipeline; when the title already salvaged, it only installs the writer and recovers
        public static void RunAtStartup(bool collectsPlaytestRecords, bool isRemoteConnection, string worldDirectory)
        {
            // 試行ごとに新しい段へ書き始める（F05）。同じプロセスでの再試行が、失敗した試行と同じセッション名へ印を上書きして証拠を消さないため
            // Every attempt starts writing in a fresh level (F05), so a retry in the same process cannot overwrite the failed attempt's marks and erase its evidence
            ProcessSessionScope.BeginNewSession();

            if (!_salvagedThisBoot)
            {
                // Editorの迂回印は読んだ時点で消費される。ここで読まないと次の手動のタイトル起動へ持ち越され、確認が1回消える
                // The Editor bypass mark is consumed on read; leaving it unread would carry it to the next manual title boot and skip its confirmations once
                var unattendedReason = PlaytestStartGateBypass.UnattendedReason();
                Debug.Log($"PreviousSessionStartupTasks: タイトルを経由しない起動のため、ここで前回セッションを退避します。同意と前回異常終了の確認はこの起動では出さず、未応答の印は次にタイトルを通る起動で聞き直します unattended:{unattendedReason ?? "none"}");
                Salvage();
            }

            // 記録を集めない起動は今回の印を書かず、前回の進行記録も回収しない。回収は次に集める起動が行う（理由はPlaytestRecordCollectionがログ済み）
            // A boot that collects nothing writes no marks of its own and leaves the leftover progress records to the next collecting boot (PlaytestRecordCollection logged why)
            if (!collectsPlaytestRecords) return;

            // 書き手は設置時に識別を読む。照合がAllowedで検証済みSteamIDを据えた後のここに置く（ADR 0065）
            // The writer reads the identity at installation, so it sits here, after the launch check set the verified SteamID on Allowed (ADR 0065)
            // 退避元はこのセッションが自分で書き残す。次に落ちたときの箱は、今回の起動設定ではなくこの記録から組まれる（D-C3）
            // This session records its own salvage source, so the box after a crash is built from this record rather than from the next boot's settings (D-C3)
            var snapshotSource = new SessionSnapshotSource(isRemoteConnection, WorldDataDirectory.FromWorldRoot(worldDirectory).SnapshotDirectory);
            CleanExitMarkWriter.InstallAtStartup(RecordingProcessDirectories.CurrentProcessId(), ProcessSessionScope.CurrentSessionName, snapshotSource);

            // 前回の書きかけの進行記録を、消費した印の結果で畳む（F19）
            // Folds the half-written progress records by the consumed marks' outcome (F19)
            ProgressSessionRecovery.RecoverLeftoverSessions(PreviousSessionSalvage.RequireArtifacts().ExitedCleanlyByProcessId);
        }

        // 呼ぶ前にこの起動のセッション名を確定しておくこと。退避は「今回以外」を畳み、書き手は全員この名前の下へ書く（F05）
        // This boot's session name must be fixed before calling: the salvage folds everything else, and every writer writes under this name (F05)
        private static PreviousSessionArtifacts Salvage()
        {
            var artifacts = PreviousSessionSalvage.RunAtStartup();
            _salvagedThisBoot = true;
            return artifacts;
        }
    }
}
