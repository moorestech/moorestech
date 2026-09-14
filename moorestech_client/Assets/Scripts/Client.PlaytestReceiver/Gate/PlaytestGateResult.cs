using Mooresmaster.Localization.Generated;

namespace Client.PlaytestReceiver.Gate
{
    public enum PlaytestGateStatus
    {
        DeveloperMode,
        Allowed,
        NotAllowed,
        Unreachable,
        TicketFailed,
    }

    // 照合の結末。止めるかどうかと、タイトルに出す理由の文言キーをここだけで決める
    // The verdict of the check; whether to stop and which reason string the title shows is decided only here
    public readonly struct PlaytestGateResult
    {
        public readonly PlaytestGateStatus Status;
        public readonly string Detail;

        public PlaytestGateResult(PlaytestGateStatus status, string detail)
        {
            Status = status;

            // 文言テンプレの{p0}へそのまま流すため、空文字への畳み込みはここ1箇所で済ませる
            // Detail feeds the {p0} placeholder directly, so the collapse to an empty string happens only here
            Detail = detail ?? "";
        }

        public bool IsBlocked => Status != PlaytestGateStatus.DeveloperMode && Status != PlaytestGateStatus.Allowed;

        // IsBlockedのときだけ読まれる。到達不能は理由を特定できなかった場合の総称なので既定に置く
        // Read only while IsBlocked; unreachable is the catch-all for a cause we could not pin down, so it is the default
        public LocalizationKey ReasonKey
        {
            get
            {
                if (Status == PlaytestGateStatus.NotAllowed) return LocalizationKeys.Ui.Playtest.NotAllowed;
                if (Status == PlaytestGateStatus.TicketFailed) return LocalizationKeys.Ui.Playtest.TicketFailed;
                return LocalizationKeys.Ui.Playtest.Unreachable;
            }
        }
    }
}
