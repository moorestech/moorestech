using Mooresmaster.Localization.Generated;

namespace Client.PlaytestReceiver.Gate
{
    public enum PlaytestGateStatus
    {
        NotEvaluated,
        DeveloperMode,
        Checking,
        Allowed,
        NotAllowed,
        Unreachable,
        TicketFailed,

        // 受け口は応答したが契約の形でない（版ずれ・受け口の不具合）。到達失敗の総称へ混ぜない
        // The receiver answered but broke the contract (version skew, receiver bug); kept apart from the unreachable catch-all
        MalformedResponse,
    }

    // 照合の結末。止めるか・理由の文言キー・許可されたセッションをここだけが持つ
    // The verdict of the check; whether to stop, which reason key to show and the allowed session live only here
    public sealed class PlaytestGateResult
    {
        public readonly PlaytestGateStatus Status;

        // ログ専用。テスター向けの文言には出さない
        // For developer logs only; never shown in the tester-facing text
        public readonly string Detail;

        private readonly PlaytestSession _allowedSession;

        // 検証済みSteamIDはAllowedの結末だけが持つ。非Allowedへ移った結果からは読めないので、識別が前の値のまま残らない（ADR 0065）
        // Only an Allowed verdict carries the verified SteamID, so a non-Allowed verdict cannot be read for one and no stale identity survives (ADR 0065)
        private readonly string _verifiedSteamId;

        private PlaytestGateResult(PlaytestGateStatus status, string detail, PlaytestSession allowedSession, string verifiedSteamId)
        {
            Status = status;
            Detail = detail ?? "";
            _allowedSession = allowedSession;
            _verifiedSteamId = verifiedSteamId;
        }

        // 未評価は止める側に倒す。判定前に開始経路が素通しできる窓を作らない
        // Not-yet-evaluated counts as blocked so no start path can slip through before the verdict
        public static PlaytestGateResult NotEvaluated => new(PlaytestGateStatus.NotEvaluated, "", null, null);
        public static PlaytestGateResult DeveloperMode => new(PlaytestGateStatus.DeveloperMode, "", null, null);
        public static PlaytestGateResult Checking => new(PlaytestGateStatus.Checking, "", null, null);

        public static PlaytestGateResult Allowed(PlaytestSession session, string verifiedSteamId)
        {
            return new PlaytestGateResult(PlaytestGateStatus.Allowed, "", session, verifiedSteamId);
        }

        public static PlaytestGateResult Blocked(PlaytestGateStatus status, string detail)
        {
            return new PlaytestGateResult(status, detail, null, null);
        }

        public bool IsBlocked => Status != PlaytestGateStatus.DeveloperMode && Status != PlaytestGateStatus.Allowed;

        // 照合の結論が出たか。未評価と照合中だけが未確定で、確定待ちと待ち文言の判定はここ1箇所に揃える
        // Whether the check has concluded; only not-evaluated and checking are unsettled, and every wait and waiting text reads this one place
        public bool IsSettled => Status != PlaytestGateStatus.NotEvaluated && Status != PlaytestGateStatus.Checking;

        public bool TryGetAllowedSession(out PlaytestSession session)
        {
            session = _allowedSession;
            return Status == PlaytestGateStatus.Allowed;
        }

        public bool TryGetVerifiedSteamId(out string verifiedSteamId)
        {
            verifiedSteamId = _verifiedSteamId;
            return Status == PlaytestGateStatus.Allowed;
        }

        // IsBlockedのときだけ読まれる。到達不能は理由を特定できなかった場合の総称なので既定に置く
        // Read only while IsBlocked; unreachable is the catch-all for a cause we could not pin down, so it is the default
        public LocalizationKey ReasonKey
        {
            get
            {
                if (!IsSettled) return LocalizationKeys.Ui.Playtest.Checking;
                if (Status == PlaytestGateStatus.NotAllowed) return LocalizationKeys.Ui.Playtest.NotAllowed;
                if (Status == PlaytestGateStatus.TicketFailed) return LocalizationKeys.Ui.Playtest.TicketFailed;
                if (Status == PlaytestGateStatus.MalformedResponse) return LocalizationKeys.Ui.Playtest.MalformedResponse;
                return LocalizationKeys.Ui.Playtest.Unreachable;
            }
        }
    }
}
