using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions.Playtest;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.Playtest;

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
        /// 応答を待つゲート一式。同意の既読・テスター識別・箱の書き出し口の解決はすべてここが持つ。
        /// The gates that wait for an answer; the consent flag, the tester identity and the writer are all resolved here.
        /// </summary>
        public static PlaytestStartGateHandles BindWaitingGates(WebSocketHub hub, PreviousSessionArtifacts artifacts)
        {
            return Bind(hub, new CrashReportGate(new CrashBundleWriter(), artifacts), PlaytestConsentFlag.IsAcknowledged());
        }

        /// <summary>
        /// 無人起動向けに、どちらも閉じた状態で登録する。登録自体を飛ばすとWeb側の購読が固着する。退避結果は借りない。
        /// Registers both gates already closed for an unattended boot without borrowing any salvage result; skipping the registration would wedge the web subscription.
        /// </summary>
        public static PlaytestStartGateHandles BindClosedGates(WebSocketHub hub)
        {
            return Bind(hub, CrashReportGate.Closed(), true);
        }

        private static PlaytestStartGateHandles Bind(WebSocketHub hub, CrashReportGate crashGate, bool consentAcknowledged)
        {
            // ゲート文言は通常の辞書経路から出す。登録がゲートより後だと辞書が届かず fallback だけが見える（ADR 0060 裁定10）
            // The gate texts come from the normal dictionary path; registering after the gates leaves only the fallback visible (ADR 0060 adjudication 10)
            LocalizationTopic.EnsureRegistered(hub);

            var consentGate = new PlaytestConsentGate(!consentAcknowledged);
            hub.RegisterTopic(PlaytestConsentGateTopic.TopicName, new PlaytestConsentGateTopic(hub, consentGate));
            PlaytestConsentGateActions.Register(hub, consentGate);

            hub.RegisterTopic(CrashReportGateTopic.TopicName, new CrashReportGateTopic(hub, crashGate));
            CrashReportGateActions.Register(hub, crashGate);

            return new PlaytestStartGateHandles { Consent = consentGate, CrashReport = crashGate };
        }
    }

    // 束ねた2ゲートの持ち手。待つ順序は開始側（PlaytestStartGates）が決めるのでここでは待たない
    // Handles to the two bound gates; the starter decides the order of the waits, so none happens here
    public sealed class PlaytestStartGateHandles
    {
        public PlaytestConsentGate Consent;
        public CrashReportGate CrashReport;

        // 待機状態の読み出しはこのアセンブリに閉じる。開始側へ渡すのは「待ちが実際に開始を止めるか」だけ
        // Reading the waiting state stays inside this assembly; only "does the wait actually block the start" crosses out
        public bool ConsentBlocksStart()
        {
            return Consent.IsWaitingAcknowledgement;
        }

        public bool CrashReportBlocksStart()
        {
            return CrashReport.IsWaitingResponse;
        }
    }
}
