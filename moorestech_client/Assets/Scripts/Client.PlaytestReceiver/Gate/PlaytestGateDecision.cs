namespace Client.PlaytestReceiver.Gate
{
    // 判定は純関数に閉じる。HTTPもSteamも触らないのでEditModeテストで全分岐を固定できる
    // The decision is a pure function; touching neither HTTP nor Steam lets EditMode tests pin every branch
    public static class PlaytestGateDecision
    {
        public static PlaytestGateResult Decide(bool hasBuildInfo, bool isSteamRunning, PlaytestSessionResult authenticated, PlaytestSession session)
        {
            var outcome = authenticated.Outcome;
            var detail = authenticated.Detail;

            // 配布ビルドの印が無い、またはSteamが動いていない = 開発者の自作ビルド。照合せずrsync経路に任せる
            // No distribution marker or no Steam means a developer's own build; skip the check and leave it to the rsync path
            if (!hasBuildInfo || !isSteamRunning) return PlaytestGateResult.DeveloperMode;

            // 検証済みSteamIDは認証の結末から受け取る。Allowedの結末は生成時にSteamIDを必ず持つ（ADR 0065）
            // The verified SteamID comes from the authentication outcome, whose Allowed factory always carries one (ADR 0065)
            if (authenticated.TryGetVerifiedSteamId(out var verifiedSteamId)) return PlaytestGateResult.Allowed(session, verifiedSteamId);

            if (outcome == PlaytestSessionOutcome.NotAllowed) return PlaytestGateResult.Blocked(PlaytestGateStatus.NotAllowed, detail);

            // 配布ビルドでSteamが動いているのにチケットが通らないのは異常。fail-closedで止める
            // A distribution build with Steam running but no usable ticket is abnormal; fail closed and stop
            if (outcome == PlaytestSessionOutcome.TicketUnavailable || outcome == PlaytestSessionOutcome.TicketRejected)
            {
                return PlaytestGateResult.Blocked(PlaytestGateStatus.TicketFailed, detail);
            }

            // 応答が契約の形でないのは到達失敗と別の理由で出す。総称の到達不能には到達失敗だけが落ちる
            // A contract-breaking answer gets its own reason; only a genuine unreachability falls through to the catch-all
            if (outcome == PlaytestSessionOutcome.MalformedResponse) return PlaytestGateResult.Blocked(PlaytestGateStatus.MalformedResponse, detail);

            return PlaytestGateResult.Blocked(PlaytestGateStatus.Unreachable, detail);
        }
    }
}
