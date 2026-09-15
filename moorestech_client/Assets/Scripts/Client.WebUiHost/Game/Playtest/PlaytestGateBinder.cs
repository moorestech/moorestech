using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions.Playtest;
using Client.WebUiHost.Game.StartGates;
using Client.WebUiHost.Game.Topics;
using UnityEngine;

namespace Client.WebUiHost.Game.Playtest
{
    /// <summary>
    /// - プレイテストの開始ゲート（topicとaction）をHubへ束ねるfacade
    /// - WebUiGameBinderより前に呼ぶ
    /// - Binds the playtest start gates' topics and actions to the Hub
    /// - Must be called before WebUiGameBinder
    /// </summary>
    public static class PlaytestGateBinder
    {
        /// <summary>
        /// 起動時の唯一の登録口。無人起動かの判定もここが持ち、登録自体は常に行う（飛ばすとWeb側の購読が固着する）。
        /// The single boot-time registration port; it owns the unattended decision and always registers, since skipping would wedge the web subscription.
        /// </summary>
        public static PlaytestStartGateHandles BindForBoot(WebSocketHub hub, PreviousSessionArtifacts artifacts)
        {
            // 無人起動（バッチモード・テスト・プレイテストDSL）には応答者が居ない。待つと恒久停止するので閉じたゲートだけ登録する
            // An unattended boot (batch mode, tests, the playtest DSL) has nobody to answer; waiting would halt forever, so only closed gates are registered
            var unattendedReason = PlaytestStartGateBypass.UnattendedReason();
            if (unattendedReason == null) return BindWaitingGates(hub, artifacts);

            Debug.LogWarning($"PlaytestGateBinder: 無人起動のため開始ゲートを出さずに進みます reason:{unattendedReason} previousExitWasClean:{artifacts.PreviousExitWasClean}（退避物は last-session に残り次回の対話起動で聞き直せます）");
            return BindClosedGates(hub);
        }

        // 応答を待つゲート一式。同意の既読・箱の書き出し口の解決はすべてここが持つ
        // The gates that wait for an answer; the consent flag and the writer are resolved here
        private static PlaytestStartGateHandles BindWaitingGates(WebSocketHub hub, PreviousSessionArtifacts artifacts)
        {
            return Bind(hub, new CrashReportGate(new CrashBundleWriter(), artifacts), PlaytestConsentFlag.IsAcknowledged());
        }

        // どちらも閉じた状態で登録する。退避結果は正常終了に偽装して借りない
        // Registers both gates already closed without borrowing a salvage result disguised as a clean exit
        private static PlaytestStartGateHandles BindClosedGates(WebSocketHub hub)
        {
            return Bind(hub, CrashReportGate.Closed(), true);
        }

        private static PlaytestStartGateHandles Bind(WebSocketHub hub, CrashReportGate crashGate, bool consentAcknowledged)
        {
            // ゲート文言は通常の辞書経路から出す。登録がゲートより後だと辞書が届かず辞書前文言だけが見える（ADR 0060 裁定10）
            // The gate texts come from the normal dictionary path; registering after the gates leaves only the pre-dictionary copy visible (ADR 0060 adjudication 10)
            LocalizationTopic.EnsureRegistered(hub);

            var consentGate = new PlaytestConsentGate(!consentAcknowledged);
            WaitingGateTopic.Register(hub, StartGateTopics.ConsentName, StartGateTopics.ConsentPrecedence, consentGate);
            PlaytestConsentGateActions.Register(hub, consentGate);

            WaitingGateTopic.Register(hub, StartGateTopics.CrashReportName, StartGateTopics.CrashReportPrecedence, crashGate);
            CrashReportGateActions.Register(hub, crashGate);

            return new PlaytestStartGateHandles(consentGate, crashGate);
        }
    }
}
