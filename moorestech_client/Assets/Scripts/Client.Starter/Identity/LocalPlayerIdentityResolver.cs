using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.PlaytestReceiver.Steam;
using Client.Starter.Initialization.Refusal;
using Game.PlayerIdentity;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Starter.Identity
{
    // 起動時の身元解決（ADR0073）
    // Startup identity resolution (ADR 0073)
    // - Steam配布ビルド=Steam / それ以外=端末値
    // - Steam distribution build=Steam / otherwise=device value
    public static class LocalPlayerIdentityResolver
    {
        internal static PlayerIdentityResolution ResolveForThisProcess()
        {
            // 配布の種別は焼き込み値で決め、実行時のSteamの状態では決めない
            // The build kind comes from the baked value, never from runtime Steam state
            var origin = RepositoryStateProbe.ReadBuildOrigin();
            return ResolveForBuildOrigin(origin, new PlaytestLocalSteamIdReader(), SystemInfo.deviceUniqueIdentifier);
        }

        internal static PlayerIdentityResolution ResolveForBuildOrigin(BuildOriginReading origin, IPlaytestLocalSteamIdReader steamReader, string deviceUniqueIdentifier)
        {
            if (origin.Kind == BuildOriginKind.BuildWithoutInfo)
            {
                return PlayerIdentityResolution.Refused(LocalizationKeys.Ui.Loading.BuildOriginUnavailable, $"build-info を読めないため身元を決められません: {origin.MissingReason}");
            }
            // 焼き込みビルドでSteamラベルを持つときだけSteam身元。新しいBuildOriginKindを足せばここで網羅漏れが見える
            // Only a baked build carrying a Steam label uses the Steam identity; a new BuildOriginKind surfaces its gap right here
            var source = origin.Kind == BuildOriginKind.BakedBuild && !string.IsNullOrEmpty(origin.BuildInfo.SteamBuildLabel)
                ? PlayerIdentitySource.SteamDistribution
                : PlayerIdentitySource.Device;
            return Resolve(source, steamReader, deviceUniqueIdentifier);
        }

        internal static PlayerIdentityResolution Resolve(PlayerIdentitySource source, IPlaytestLocalSteamIdReader steamReader, string deviceUniqueIdentifier)
        {
            if (source == PlayerIdentitySource.SteamDistribution)
            {
                if (steamReader.TryRead(out var steamId, out var failureReason)) return PlayerIdentityResolution.Success(PlayerIdentityText.SteamPrefix + steamId);
                return PlayerIdentityResolution.Refused(LocalizationKeys.Ui.Loading.SteamIdentityUnavailable, $"Steam配布ビルドでSteamIDを読めないため開始しない: {failureReason}");
            }

            if (string.IsNullOrEmpty(deviceUniqueIdentifier) || deviceUniqueIdentifier == SystemInfo.unsupportedIdentifier)
            {
                return PlayerIdentityResolution.Refused(LocalizationKeys.Ui.Loading.DeviceIdentityUnavailable, $"端末の識別子を取得できないため開始しない: '{deviceUniqueIdentifier}'");
            }
            return PlayerIdentityResolution.Success(PlayerIdentityText.ForDevice(deviceUniqueIdentifier));
        }
    }

    public readonly struct PlayerIdentityResolution
    {
        public readonly string Identity;
        public readonly PlayerStartRefusal? Refusal;

        private PlayerIdentityResolution(string identity, PlayerStartRefusal? refusal)
        {
            Identity = identity;
            Refusal = refusal;
        }

        internal static PlayerIdentityResolution Success(string identity) => new(identity, null);
        internal static PlayerIdentityResolution Refused(LocalizationKey localizationKey, string logReason) => new(null, new PlayerStartRefusal(localizationKey, logReason));
    }
}
