using System.IO;
using Client.Game.InGame.BugReport.Playtest;
using Client.PlaytestReceiver.Steam;
using Game.Paths;
using UnityEngine;

namespace Client.PlaytestReceiver.Launch
{
    // 配布版かどうかをプロセスで1回だけ判定し、配布版ならローカルSteamのSteamIDを識別へ差し込む（ADR 0070）
    // Decides once per process whether this is a distribution build, and pushes the local Steam SteamID into the identity if so (ADR 0070)
    public static class PlaytestLaunchProfile
    {
        // MainMenuにはDIコンテナが無く、開始経路・走行役・タイトルが別々に読むのでstaticで持つ
        // The MainMenu has no DI container and the start paths, runner and title read it separately, so it is held statically
        private static PlaytestLaunchKind _kind = PlaytestLaunchKind.NotEvaluated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayMode()
        {
            // 前回の再生の識別を次の判定前へ持ち越さない
            // Do not carry the previous play session identity into the next unresolved boot
            if (!string.IsNullOrEmpty(PlaytestSessionIdentityProvider.Current.SteamId))
            {
                Debug.Log("[PlaytestReceiver] resetting local SteamID for the next launch");
            }
            Apply(PlaytestLaunchKind.NotEvaluated, "");
        }

        public static PlaytestLaunchKind Resolve()
        {
            if (_kind != PlaytestLaunchKind.NotEvaluated) return _kind;
            if (!IsDistributionBuild())
            {
                Apply(PlaytestLaunchKind.DeveloperMode, "");
                return _kind;
            }
            // 読めなくても開始は止めない。追跡の正は送信時トークンが決めるR2の置き場所
            // A failed read never stops the boot; the R2 location set by the send-time token is the tracking authority
            if (!PlaytestLocalSteamIdReader.TryRead(out var steamId, out var failureReason)) Debug.LogWarning($"[PlaytestReceiver] 記録のSteamIDを空で続行します: {failureReason}");
            Apply(PlaytestLaunchKind.Distribution, steamId);
            return _kind;

            #region Internal

            bool IsDistributionBuild()
            {
                // 開発者モードへ倒す経路も理由をログへ残す。無音だと配布版で送信が止まっても気づけない
                // The developer-mode fallback is logged too; silently, a distribution build that stopped shipping would go unnoticed
                if (!File.Exists(GameSystemPaths.BuildInfoFilePath))
                {
                    Debug.Log("[PlaytestReceiver] developer mode (no build-info.json)");
                    return false;
                }
                if (!new PlaytestSteamTicketProvider().IsSteamRunning())
                {
                    Debug.Log("[PlaytestReceiver] developer mode (Steam is not running)");
                    return false;
                }
                return true;
            }

            #endregion
        }

        internal static void SetForTest(PlaytestLaunchKind kind, string steamId)
        {
            Apply(kind, steamId);
        }

        internal static void ResetForTest()
        {
            ResetOnPlayMode();
        }

        private static void Apply(PlaytestLaunchKind kind, string steamId)
        {
            _kind = kind;
            // 識別の設定は判定と同じ1箇所で行う。開発者モードと読めなかった配布版は理由付きの空にする
            // The identity is set in the same single place as the decision; developer mode and an unreadable distribution get a reasoned empty one
            if (kind == PlaytestLaunchKind.Distribution && !string.IsNullOrEmpty(steamId)) PlaytestSessionIdentityProvider.SetCurrent(new LocalSteamSessionIdentity(steamId));
            else if (kind == PlaytestLaunchKind.DeveloperMode) PlaytestSessionIdentityProvider.SetCurrent(new EmptyPlaytestSessionIdentity(EmptyPlaytestSessionIdentity.DeveloperModeReason));
            else if (kind == PlaytestLaunchKind.Distribution) PlaytestSessionIdentityProvider.SetCurrent(new EmptyPlaytestSessionIdentity("テスター識別（SteamID）が無い（SteamUser.GetSteamID で読めなかった）"));
            else PlaytestSessionIdentityProvider.SetCurrent(new EmptyPlaytestSessionIdentity("テスター識別（SteamID）が無い（起動時のローカルSteamIDがまだ差し込まれていない）"));
        }
    }
}
