using System;

namespace Client.PlaytestReceiver
{
    public enum PlaytestSessionOutcome
    {
        Authenticated,
        TicketRejected,
        TicketUnavailable,
        Unreachable,

        // 受け口は200で応答したが本文が契約の形でない（版ずれ・キャプティブポータル等）。到達失敗と混ぜない
        // The receiver answered 200 but the body breaks the contract (version skew, captive portal); kept apart from unreachability
        MalformedResponse,
    }

    // 成功だけが検証済み応答を持ち、失敗だけが理由を持つ
    // Only success carries a validated response and only failure carries an error
    public abstract class PlaytestSessionResult
    {
        private protected PlaytestSessionResult() { }
        public static PlaytestSessionFailed Failed(PlaytestSessionOutcome outcome, string detail)
        { return new PlaytestSessionFailed(outcome, detail); }
    }

    public sealed class PlaytestSessionAuthenticated : PlaytestSessionResult
    {
        public readonly Client.PlaytestReceiver.Http.Responses.PlaytestSessionResponse Response;
        internal PlaytestSessionAuthenticated(Client.PlaytestReceiver.Http.Responses.PlaytestSessionResponse response) { Response = response; }
    }

    public sealed class PlaytestSessionFailed : PlaytestSessionResult
    {
        public readonly PlaytestSessionOutcome Outcome;
        public readonly string Detail;
        internal PlaytestSessionFailed(PlaytestSessionOutcome outcome, string detail)
        {
            if (outcome == PlaytestSessionOutcome.Authenticated) throw new ArgumentException("Failure cannot be authenticated", nameof(outcome));
            Outcome = outcome;
            Detail = detail;
        }
    }
}
