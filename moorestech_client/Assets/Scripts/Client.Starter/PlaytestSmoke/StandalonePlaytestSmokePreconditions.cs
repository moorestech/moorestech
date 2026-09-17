using System.IO;
using Client.Game.InGame.BugReport.Playtest;
using Client.PlaytestReceiver.Gate;
using Game.Paths;

namespace Client.Starter.PlaytestSmoke
{
    /// <summary>
    /// 無人では越えられない関門を開始前に検出し、止まる代わりに理由付きで失敗させる
    /// Detects gates nobody can pass unattended before starting, so the run fails with a reason instead of stalling
    /// </summary>
    public static class StandalonePlaytestSmokePreconditions
    {
        // 越えられない関門があればその理由を、無ければ空文字を返す
        // Returns the reason when a gate cannot be passed, or an empty string when all hold
        internal static string FindFailure(StandalonePlaytestSmokeSettings settings, PlaytestGateResult gate)
        {
            // 通し検証の対象は照合を通った配布版だけ。開発者モードでは報告が受け口へ運ばれない
            // Only a checked distribution build is in scope; in developer mode no report ever reaches the receiver
            if (gate.Status != PlaytestGateStatus.Allowed)
                return $"launch gate is {gate.Status} (Allowed required; developer mode means build-info.json is missing or Steam is not running) {gate.Detail}";

            // 同意表示は応答を上限なく待つ。検証機では初回セットアップで一度だけ人が既読にする
            // The consent notice waits without bound; on the verifier a human acknowledges it once during setup
            if (!PlaytestConsentFlag.IsAcknowledged())
                return $"the playtest consent notice has not been acknowledged on this machine ({PlaytestConsentFlag.FilePath}); acknowledge it once interactively";

            // phase2はphase1のセーブを読む検証。ワールドが無いと新規生成され、ロードを確かめないまま進んでしまう
            // phase2 verifies loading phase1's save; without the world a fresh one is generated and loading goes unverified
            if (settings.Phase == StandalonePlaytestSmokeSettings.PhaseTwo && !Directory.Exists(GameSystemPaths.DefaultWorldDirectory))
                return $"no saved world to load at {GameSystemPaths.DefaultWorldDirectory}; run phase1 first";

            return "";
        }
    }
}
