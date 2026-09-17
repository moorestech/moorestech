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
        // 越えられない関門があれば true とその理由を返す
        // Returns true with the reason when a gate cannot be passed
        internal static bool TryFindFailure(StandalonePlaytestSmokeSettings settings, PlaytestGateResult gate, out string failureReason)
        {
            // 通し検証の対象は照合を通った配布版だけ。開発者モードでは報告が受け口へ運ばれない
            // Only a checked distribution build is in scope; in developer mode no report ever reaches the receiver
            if (gate.Status != PlaytestGateStatus.Allowed)
            {
                failureReason = $"launch gate is {gate.Status} (Allowed required; developer mode means build-info.json is missing or Steam is not running) {gate.Detail}";
                return true;
            }

            // 同意表示は応答を上限なく待つ。検証機では初回セットアップで一度だけ人が既読にする
            // The consent notice waits without bound; on the verifier a human acknowledges it once during setup
            if (!PlaytestConsentFlag.IsAcknowledged())
            {
                failureReason = $"the playtest consent notice has not been acknowledged on this machine ({PlaytestConsentFlag.FilePath}); acknowledge it once interactively";
                return true;
            }

            switch (settings.Phase)
            {
                case StandalonePlaytestSmokePhase.PhaseOne:
                    failureReason = "";
                    return false;
                case StandalonePlaytestSmokePhase.PhaseTwo:
                    // phase2はphase1のセーブを読む検証。ワールドが無いと新規生成され、ロードを確かめないまま進んでしまう
                    // phase2 verifies loading phase1's save; without the world a fresh one is generated and loading goes unverified
                    if (Directory.Exists(GameSystemPaths.DefaultWorldDirectory))
                    {
                        failureReason = "";
                        return false;
                    }
                    failureReason = $"no saved world to load at {GameSystemPaths.DefaultWorldDirectory}; run phase1 first";
                    return true;
                default:
                    failureReason = $"unknown smoke phase {settings.Phase}";
                    return true;
            }
        }
    }
}
