using System;

namespace Client.PlaytestReceiver
{
    public enum PlaytestSessionOutcome
    {
        Allowed,
        TicketRejected,
        TicketUnavailable,
        Unreachable,

        // 受け口は200で応答したが本文が契約の形でない（版ずれ・キャプティブポータル等）。到達失敗と混ぜない
        // The receiver answered 200 but the body breaks the contract (version skew, captive portal); kept apart from unreachability
        MalformedResponse,
    }

    // 認証1回の結末。生成口は成功と失敗の2本に絞る
    // The outcome of one authentication has separate success and failure factories
    public sealed class PlaytestSessionResult
    {
        public PlaytestSessionOutcome Outcome { get; }

        // ログ専用。テスター向けの文言には出さない
        // For developer logs only; never shown to testers
        public string Detail { get; }

        private PlaytestSessionResult(PlaytestSessionOutcome outcome, string detail)
        {
            Outcome = outcome;
            Detail = detail ?? "";
        }

        public static PlaytestSessionResult Allowed()
        {
            return new PlaytestSessionResult(PlaytestSessionOutcome.Allowed, "");
        }

        public static PlaytestSessionResult Failed(PlaytestSessionOutcome outcome, string detail)
        {
            if (outcome == PlaytestSessionOutcome.Allowed) throw new ArgumentException("a failed session result cannot be Allowed", nameof(outcome));
            return new PlaytestSessionResult(outcome, detail);
        }
    }
}
