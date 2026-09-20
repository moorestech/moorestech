using UnityEngine;

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

            // 検証済みSteamIDは認証の結末から受け取る。ここで作られたAllowedだけが識別を持つ（ADR 0065）
            // The verified SteamID comes from the authentication outcome, so only the Allowed built here carries an identity (ADR 0065)
            if (outcome == PlaytestSessionOutcome.Allowed)
            {
                // Allowedなのに検証済みSteamIDが無いのは受け口の契約違反。通すと識別が空のまま進行記録・manifest・異常終了箱へ載る（ADR 0065）
                // An Allowed without a verified SteamID breaks the receiver's contract; letting it pass would carry an empty identity into records, manifests and crash boxes (ADR 0065)
                if (string.IsNullOrEmpty(authenticated.SteamId))
                {
                    Debug.LogError("[PlaytestReceiver] allowed without a verified steamId; treating it as a contract breach and blocking the launch");
                    return PlaytestGateResult.Blocked(PlaytestGateStatus.Unreachable, "allowed without a verified steamId");
                }
                return PlaytestGateResult.Allowed(session, authenticated.SteamId);
            }

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
