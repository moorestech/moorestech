using System.Threading;
using Client.Common;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Submit;
using Client.Localization;
using Client.PlaytestReceiver.Gate;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter.Playtest.TitleGates
{
    /// <summary>
    /// 開始の関所。照合（PlaytestLaunchGate）→ タイトルのゲート（同意・前回異常終了の確認）の2段の順序をここに閉じ、
    /// 開始経路（Play locally・サーバー接続・パイプラインの漏斗）はこの1箇所だけを呼ぶ（ADR 0065）。
    /// The start checkpoint; it closes the order of the two stages (the launch check, then the title gates for consent and the previous crash) inside itself,
    /// and every start path (Play locally, server connection, the pipeline funnel) calls only this one place (ADR 0065).
    /// </summary>
    public static class PlaytestTitleGates
    {
        // MainMenuシーンにはDIコンテナが無く、開始経路と表示が別のMonoBehaviourなのでstaticで持つ（前例: PlaytestLaunchGate）
        // The MainMenu scene has no DI container and the start paths and the view are separate MonoBehaviours, so it is held statically (precedent: PlaytestLaunchGate)
        // 段階の持ち主は列そのもの。ここは「今動いている列」だけを持ち、列が無いことが「まだ始まっていない」を表す
        // The step's owner is the sequence itself; this holds only the running one, and its absence is what "not started yet" means
        private static PlaytestTitleGateSequence _current;

        // Editorの再生し直しは同じプロセスで起動をやり直すため、再生ごとに未開始へ戻す（前例: PlaytestLaunchGate）
        // An Editor replay restarts the boot in the same process, so each play returns to "not started" (precedent: PlaytestLaunchGate)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetOnPlayMode()
        {
            _current = null;
        }

        // タイトル以外のシーンから始まる起動（Editorの直接再生・プレイテストDSL・smoke）はゲートを出す画面が無い。
        // 漏斗より前のここで明示的に通し、未応答の印は次にタイトルを通る起動で聞き直す（D1 裁定）
        // A boot starting outside the title (an Editor direct play, the playtest DSL, the smoke run) has no screen to show the gates on;
        // it is passed explicitly here, before the funnel, and the unanswered marks are asked again at the next boot through the title (D1 adjudication)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void MarkPassedWhenBootingOutsideTitle()
        {
            var bootSceneName = SceneManager.GetActiveScene().name;
            if (bootSceneName == SceneConstant.MainMenuSceneName) return;
            PlaytestStartGateBypass.DeclareDirectBoot($"起動シーンがタイトルではない（{bootSceneName}）");
        }

        // 照合がAllowedか開発者モードに決まるたびにタイトルの合成ルートから呼ぶ。始動済みなら既存の列をそのまま返す
        // Called from the title's composition root whenever the launch check settles as Allowed or developer mode; an already-started sequence comes back as it is
        public static bool TryBegin(PlaytestGateResult verdict, IPlaytestUploadRequester uploadRequester, out PlaytestTitleGateSequence sequence)
        {
            // 始動済みなら既存の列を返す。初期化失敗でタイトルへ戻った再訪でも、未応答の確認を繋ぎ直して出せる（D-C1）
            // An already-started sequence comes back so a revisit after a failed initialization can re-attach and show the unanswered confirmation (D-C1)
            sequence = _current;
            if (sequence != null)
            {
                // 列は生き残るが合成ルートは再訪のたびに作り直される。送り手だけ今回のタイトルのものへ繋ぎ直す（D-C1）
                // The sequence survives while the composition root is rebuilt on every revisit, so only the requester is re-attached to this title's one (D-C1)
                sequence.SetUploadRequester(uploadRequester);
                return true;
            }

            if (verdict.IsBlocked)
            {
                Debug.LogWarning($"[PlaytestTitleGates] 照合を通っていない結果（{verdict.Status}）ではタイトルのゲートを始めません");
                return false;
            }

            // 待ちの寿命はプロセスへ揃える。タイトルが破棄されても列は生き残り、再訪で同じ確認を答えられる（D-C1）
            // The wait lives as long as the process, so the sequence survives the title's teardown and the same confirmation can be answered on a revisit (D-C1)
            var artifacts = PreviousSessionStartupTasks.SalvageAtTitle();
            sequence = BeginComposed(artifacts, verdict.TryGetAllowedSession(out _), uploadRequester, PlaytestStartGateBypass.UnattendedReason(), Application.exitCancellationToken);
            return true;
        }

        // 2段の順序はここに閉じる。照合の遅延確定が同期でタイトルのゲートを始めるため、照合を先に通さないと段階が読めない
        // The two stages' order is closed in here: the launch check's lazy settling starts the title gates synchronously, so the step cannot be read before it
        public static PlaytestStartVerdict EvaluateStart(string callerName, out PlaytestStartRefusal refusal)
        {
            refusal = new PlaytestStartRefusal("");
            if (!PlaytestLaunchGate.TryPassLaunchCheck(callerName, out var launchDenyReasonText))
            {
                refusal = new PlaytestStartRefusal(launchDenyReasonText);
                return PlaytestStartVerdict.RefusedWithNotice;
            }

            if (_current == null)
            {
                // タイトルを通らない起動の明示通過。列が始まればそちらが段階の正本になる（タイトルへ戻れば未応答の確認は出し直す）
                // The explicit pass for a boot that skips the title; once a sequence starts it is the authority (a return to the title still asks the unanswered confirmations)
                if (PlaytestStartGateBypass.DirectBootReason() != null) return PlaytestStartVerdict.Passed;

                // 確認が画面に出ていないのに断る経路。無音だと押しても何も起きないので、テスターに読める文言を返す
                // A refusal with no confirmation on screen; staying silent would make the button do nothing, so a tester-readable text comes back
                Debug.LogWarning($"[PlaytestTitleGates] {callerName} refused: the title gates never started (the launch verdict has not reached the title yet)");
                refusal = new PlaytestStartRefusal(Localize.Get(LocalizationKeys.Ui.Playtest.Gate.NotStarted));
                return PlaytestStartVerdict.RefusedWithNotice;
            }

            var step = _current.Step.Value;
            if (step == PlaytestTitleGateStep.Passed) return PlaytestStartVerdict.Passed;

            // 断った理由は開発者ログへ出す。答えるべき確認は画面に出ているので、テスター向けの文言は足さない
            // The refusal goes to the developer log; the pending confirmation is already on screen, so no tester-facing text is added
            Debug.LogWarning($"[PlaytestTitleGates] {callerName} refused: the title gates are at {step} (answer the consent / previous-crash confirmation first)");
            return PlaytestStartVerdict.RefusedWhileConfirmationVisible;
        }

        // 組んだ列を現行として据えてから進める唯一の入口。CIはバッチモードで常に無人なので、無人の理由は引数で受けて対話起動もテストで組めるようにする
        // The single entry that installs the composed sequence as the running one before advancing it; CI is always unattended in batch mode, so the reason is a parameter and tests can build an attended boot too
        internal static PlaytestTitleGateSequence BeginComposed(PreviousSessionArtifacts artifacts, bool receiverSessionAllowed, IPlaytestUploadRequester uploadRequester, string unattendedReason, CancellationToken ct)
        {
            var sequence = Compose(artifacts, receiverSessionAllowed, uploadRequester, unattendedReason);
            SetCurrentSequence(sequence);
            sequence.RunAsync(ct).Forget();
            return sequence;
        }

        // 退避結果・照合・無人の理由からゲート一式を組む
        // Builds the gate set from the salvage result, the check and the unattended reason
        private static PlaytestTitleGateSequence Compose(PreviousSessionArtifacts artifacts, bool receiverSessionAllowed, IPlaytestUploadRequester uploadRequester, string unattendedReason)
        {
            var consentAcknowledged = PlaytestConsentFlag.IsAcknowledged();
            if (unattendedReason == null)
            {
                return new PlaytestTitleGateSequence(new PlaytestConsentGate(!consentAcknowledged), new CrashReportGate(new CrashBundleWriter(), artifacts), uploadRequester, receiverSessionAllowed);
            }

            // 無人起動には応答者が居ない。閉じたゲートで進め、未読のままなら持ち越しも送らない（「了解まで送らない」を無人でも守る）
            // An unattended boot has nobody to answer: proceed with closed gates, and ship nothing carried over while the consent is unread (the hold applies unattended too)
            Debug.LogWarning($"[PlaytestTitleGates] 無人起動のためタイトルのゲートを出さずに進みます reason:{unattendedReason} previousExitWasClean:{artifacts.PreviousExitWasClean} consentAcknowledged:{consentAcknowledged}（退避物は last-session に残り次回の対話起動で聞き直せます）");
            var uploadsEnabled = receiverSessionAllowed && consentAcknowledged;
            return new PlaytestTitleGateSequence(new PlaytestConsentGate(false), CrashReportGate.Closed(), uploadRequester, uploadsEnabled);
        }

        // 現行の列を据える唯一の窓口。段階の正本はこの列なので、据え替えは開始経路の判定と同じ場所に置く
        // The single window that installs the running sequence; the step's authority is that sequence, so installing it sits with the start-path decision
        private static void SetCurrentSequence(PlaytestTitleGateSequence sequence)
        {
            _current = sequence;
        }
    }
}
