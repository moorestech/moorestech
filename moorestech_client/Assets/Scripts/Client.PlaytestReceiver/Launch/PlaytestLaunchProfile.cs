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
        internal static void ResetOnPlayMode()
        {
            // 前回の再生の識別を次の判定前へ持ち越さない
            // Do not carry the previous play session identity into the next unresolved boot
            if (!string.IsNullOrEmpty(PlaytestSessionIdentityProvider.Current.SteamId))
            {
                Debug.Log("[PlaytestReceiver] resetting local SteamID for the next launch");
            }
            Apply(PlaytestLaunchKind.NotEvaluated, new EmptyPlaytestSessionIdentity(EmptyPlaytestSessionIdentity.NotYetResolvedReason));
        }

        public static PlaytestLaunchKind Resolve()
        {
            if (_kind != PlaytestLaunchKind.NotEvaluated) return _kind;
            return ResolveWith(File.Exists(GameSystemPaths.BuildInfoFilePath), new PlaytestSteamTicketProvider(), new PlaytestLocalSteamIdReader());
        }

        // Steam境界を差し替えられる形で解決する。テストは同じ経路で配布版の2条件をmutation可能な形で検査できる（C4）
        // Resolves with substitutable Steam boundaries so tests can mutate both distribution conditions through the same path (C4)
        internal static PlaytestLaunchKind ResolveWith(bool buildInfoExists, IPlaytestSteamTicketProvider steam, IPlaytestLocalSteamIdReader reader)
        {
            var kind = Decide(buildInfoExists, steam);
            if (kind != PlaytestLaunchKind.Distribution)
            {
                Apply(kind, new EmptyPlaytestSessionIdentity(EmptyPlaytestSessionIdentity.DeveloperModeReason));
                return _kind;
            }

            // 読めなくても開始は止めない。追跡の正は送信時トークンが決めるR2の置き場所
            // A failed read never stops the boot; the R2 location set by the send-time token is the tracking authority
            if (!reader.TryRead(out var steamId, out var failureReason))
            {
                Debug.LogWarning($"[PlaytestReceiver] 記録のSteamIDを空で続行します: {failureReason}");
                Apply(kind, new EmptyPlaytestSessionIdentity($"{EmptyPlaytestSessionIdentity.LocalSteamIdUnreadableReason}: {failureReason}"));
                return _kind;
            }
            Apply(kind, new LocalSteamSessionIdentity(steamId));
            return _kind;
        }

        // 配布版か開発者モードかだけを判定する純関数。Steam境界をmutationテストで検査できるよう分離する（C4）
        // A pure decision between distribution and developer mode, split out so the Steam boundary is mutation-testable (C4)
        internal static PlaytestLaunchKind Decide(bool buildInfoExists, IPlaytestSteamTicketProvider steam)
        {
            // 開発者モードへ倒す経路も理由をログへ残す。無音だと配布版で送信が止まっても気づけない
            // The developer-mode fallback is logged too; silently, a distribution build that stopped shipping would go unnoticed
            if (!buildInfoExists)
            {
                Debug.Log("[PlaytestReceiver] developer mode (no build-info.json)");
                return PlaytestLaunchKind.DeveloperMode;
            }
            if (!steam.IsSteamRunning())
            {
                Debug.Log("[PlaytestReceiver] developer mode (Steam is not running)");
                return PlaytestLaunchKind.DeveloperMode;
            }
            return PlaytestLaunchKind.Distribution;
        }

        // 識別を組む場所は呼び出し側（Resolve系・ResetOnPlayMode）に一元化する。理由を捨てず型で運ぶ（C5）
        // Identity construction is centralized in the callers (the resolve paths and ResetOnPlayMode); the reason travels through the type instead of being discarded (C5)
        internal static void Apply(PlaytestLaunchKind kind, IPlaytestSessionIdentity identity)
        {
            _kind = kind;
            PlaytestSessionIdentityProvider.SetCurrent(identity);
        }
    }
}
