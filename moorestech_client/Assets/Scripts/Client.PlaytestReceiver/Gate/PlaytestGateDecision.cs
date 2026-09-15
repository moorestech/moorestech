namespace Client.PlaytestReceiver.Gate
{
    // 判定は純関数に閉じる。HTTPもSteamも触らないのでEditModeテストで全分岐を固定できる
    // The decision is a pure function; touching neither HTTP nor Steam lets EditMode tests pin every branch
    public static class PlaytestGateDecision
    {
        public static PlaytestGateResult Decide(bool hasBuildInfo, bool isSteamRunning, PlaytestSessionOutcome outcome, string detail, PlaytestSession session)
        {
            // 配布ビルドの印が無い、またはSteamが動いていない = 開発者の自作ビルド。照合せずrsync経路に任せる
            // No distribution marker or no Steam means a developer's own build; skip the check and leave it to the rsync path
            if (!hasBuildInfo || !isSteamRunning) return PlaytestGateResult.DeveloperMode;

            if (outcome == PlaytestSessionOutcome.Allowed) return PlaytestGateResult.Allowed(session);
            if (outcome == PlaytestSessionOutcome.NotAllowed) return PlaytestGateResult.Blocked(PlaytestGateStatus.NotAllowed, detail);

            // 配布ビルドでSteamが動いているのにチケットが通らないのは異常。fail-closedで止める
            // A distribution build with Steam running but no usable ticket is abnormal; fail closed and stop
            if (outcome == PlaytestSessionOutcome.TicketUnavailable || outcome == PlaytestSessionOutcome.TicketRejected)
            {
                return PlaytestGateResult.Blocked(PlaytestGateStatus.TicketFailed, detail);
            }

            return PlaytestGateResult.Blocked(PlaytestGateStatus.Unreachable, detail);
        }
    }
}
