using System;
using System.Threading;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Submit;
using Client.PlaytestReceiver.Gate;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.Starter.Playtest.TitleGates
{
    /// <summary>
    /// タイトルのゲートの唯一の窓口。照合通過で1回だけ始め、開始経路（Play locally・サーバー接続）はここへ問い合わせる（ADR 0065）。
    /// The single window onto the title gates; begun once when the launch check passes, and the start paths (Play locally, server connection) ask here (ADR 0065).
    /// </summary>
    public static class PlaytestTitleGates
    {
        // MainMenuシーンにはDIコンテナが無く、開始経路と表示が別のMonoBehaviourなのでstaticで持つ（前例: PlaytestLaunchGate）
        // The MainMenu scene has no DI container and the start paths and the view are separate MonoBehaviours, so it is held statically (precedent: PlaytestLaunchGate)
        private static readonly ReactiveProperty<PlaytestTitleGateStep> StepProperty = new(PlaytestTitleGateStep.NotStarted);
        public static IReadOnlyReactiveProperty<PlaytestTitleGateStep> Step => StepProperty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayMode()
        {
            SetStep(PlaytestTitleGateStep.NotStarted);
        }

        // 照合がAllowedか開発者モードに決まった直後に、タイトルの合成ルートから1回だけ呼ぶ
        // Called once from the title's composition root right after the launch check settles as Allowed or developer mode
        public static PlaytestTitleGateSequence Begin(PlaytestGateResult verdict, IPlaytestUploadRequester uploadRequester, CancellationToken ct)
        {
            if (StepProperty.Value != PlaytestTitleGateStep.NotStarted) throw new InvalidOperationException($"PlaytestTitleGates: タイトルのゲートは起動1回に1度だけ始めます（現在 {StepProperty.Value}）");
            if (verdict.IsBlocked) throw new InvalidOperationException($"PlaytestTitleGates: 照合を通っていない結果（{verdict.Status}）ではタイトルのゲートを始めません");

            var artifacts = PreviousSessionStartupTasks.SalvageAtTitle();
            var sequence = Compose(artifacts, verdict.TryGetAllowedSession(out _), uploadRequester, PlaytestStartGateBypass.UnattendedReason());
            sequence.RunAsync(ct).Forget();
            return sequence;
        }

        public static bool TryPassStart(string callerName)
        {
            var step = StepProperty.Value;
            if (step == PlaytestTitleGateStep.Passed) return true;

            // 断った理由は開発者ログへ出す。答えるべき確認は画面に出ているので、テスター向けの文言は足さない
            // The refusal goes to the developer log; the pending confirmation is already on screen, so no tester-facing text is added
            Debug.LogWarning($"[PlaytestTitleGates] {callerName} refused: the title gates are at {step} (answer the consent / previous-crash confirmation first)");
            return false;
        }

        // 退避結果・照合・無人の理由からゲート一式を組む。CIはバッチモードで常に無人なので、無人の理由は引数で受けて対話起動もテストで組めるようにする
        // Builds the gate set from the salvage result, the check and the unattended reason; CI is always unattended in batch mode, so the reason is a parameter and tests can build an attended boot too
        internal static PlaytestTitleGateSequence Compose(PreviousSessionArtifacts artifacts, bool receiverSessionAllowed, IPlaytestUploadRequester uploadRequester, string unattendedReason)
        {
            var consentAcknowledged = PlaytestConsentFlag.IsAcknowledged();
            if (unattendedReason == null)
            {
                return new PlaytestTitleGateSequence(StepProperty, new PlaytestConsentGate(!consentAcknowledged), new CrashReportGate(new CrashBundleWriter(), artifacts), uploadRequester, receiverSessionAllowed);
            }

            // 無人起動には応答者が居ない。閉じたゲートで進め、未読のままなら持ち越しも送らない（「了解まで送らない」を無人でも守る）
            // An unattended boot has nobody to answer: proceed with closed gates, and ship nothing carried over while the consent is unread (the hold applies unattended too)
            Debug.LogWarning($"[PlaytestTitleGates] 無人起動のためタイトルのゲートを出さずに進みます reason:{unattendedReason} previousExitWasClean:{artifacts.PreviousExitWasClean} consentAcknowledged:{consentAcknowledged}（退避物は last-session に残り次回の対話起動で聞き直せます）");
            var uploadsEnabled = receiverSessionAllowed && consentAcknowledged;
            return new PlaytestTitleGateSequence(StepProperty, new PlaytestConsentGate(false), CrashReportGate.Closed(), uploadRequester, uploadsEnabled);
        }

        internal static void SetStep(PlaytestTitleGateStep step)
        {
            StepProperty.Value = step;
        }
    }
}
