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

    // 認証1回の結末。生成口はAllowed/Failedの2本に絞り、検証済みSteamIDの無いAllowedを作れなくする
    // The outcome of one authentication; only the Allowed and Failed factories build it, so an Allowed without a verified SteamID cannot exist
    public sealed class PlaytestSessionResult
    {
        public PlaytestSessionOutcome Outcome { get; }

        // ログ専用。テスター向けの文言には出さない
        // For developer logs only; never shown to testers
        public string Detail { get; }

        private readonly string _verifiedSteamId;

        private PlaytestSessionResult(PlaytestSessionOutcome outcome, string detail, string verifiedSteamId)
        {
            Outcome = outcome;
            Detail = detail ?? "";
            _verifiedSteamId = verifiedSteamId;
        }

        // 検証済みSteamIDはここでしか載らない。空のまま識別へ流れる経路を型で塞ぐ（ADR 0065）
        // The verified SteamID enters only here, so no path can carry an empty identity onward (ADR 0065)
        public static PlaytestSessionResult Allowed(string verifiedSteamId)
        {
            if (string.IsNullOrWhiteSpace(verifiedSteamId)) throw new ArgumentException("an allowed session result needs a verified steamId", nameof(verifiedSteamId));
            return new PlaytestSessionResult(PlaytestSessionOutcome.Allowed, "", verifiedSteamId);
        }

        public static PlaytestSessionResult Failed(PlaytestSessionOutcome outcome, string detail)
        {
            if (outcome == PlaytestSessionOutcome.Allowed) throw new ArgumentException("a failed session result cannot be Allowed", nameof(outcome));
            return new PlaytestSessionResult(outcome, detail, null);
        }

        public bool TryGetVerifiedSteamId(out string verifiedSteamId)
        {
            verifiedSteamId = _verifiedSteamId;
            return Outcome == PlaytestSessionOutcome.Allowed;
        }
    }
}
