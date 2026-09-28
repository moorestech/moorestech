using System;
using System.Security.Cryptography;
using System.Text;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.PlaytestReceiver.Steam;
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
            var isSteamDistribution = origin.Kind == BuildOriginKind.BakedBuild && !string.IsNullOrEmpty(origin.BuildInfo.SteamBuildLabel);
            return Resolve(isSteamDistribution, new PlaytestLocalSteamIdReader(), SystemInfo.deviceUniqueIdentifier);
        }

        public static PlayerIdentityResolution Resolve(bool isSteamDistributionBuild, IPlaytestLocalSteamIdReader steamReader, string deviceUniqueIdentifier)
        {
            if (isSteamDistributionBuild)
            {
                if (steamReader.TryRead(out var steamId, out var failureReason)) return PlayerIdentityResolution.Success(PlayerIdentityText.SteamPrefix + steamId);
                return PlayerIdentityResolution.Refused(LocalizationKeys.Ui.Loading.SteamIdentityUnavailable, $"Steam配布ビルドでSteamIDを読めないため開始しない: {failureReason}");
            }

            if (string.IsNullOrEmpty(deviceUniqueIdentifier) || deviceUniqueIdentifier == SystemInfo.unsupportedIdentifier)
            {
                return PlayerIdentityResolution.Refused(LocalizationKeys.Ui.Loading.DeviceIdentityUnavailable, $"端末の識別子を取得できないため開始しない: '{deviceUniqueIdentifier}'");
            }
            return PlayerIdentityResolution.Success(PlayerIdentityText.DevicePrefix + Sha256Hex(deviceUniqueIdentifier));

            #region Internal

            // 生の端末識別子をサーバーやセーブへ出さないためハッシュ化する
            // Hash so the raw device identifier never reaches a server or a save
            string Sha256Hex(string text)
            {
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }

            #endregion
        }
    }

    public readonly struct PlayerIdentityResolution
    {
        public readonly bool Succeeded;
        public readonly string Identity;
        public readonly LocalizationKey RefusalLocalizationKey;
        public readonly string RefusalLogReason;

        private PlayerIdentityResolution(bool succeeded, string identity, LocalizationKey refusalLocalizationKey, string refusalLogReason)
        {
            Succeeded = succeeded;
            Identity = identity;
            RefusalLocalizationKey = refusalLocalizationKey;
            RefusalLogReason = refusalLogReason;
        }

        internal static PlayerIdentityResolution Success(string identity) => new(true, identity, default, null);
        internal static PlayerIdentityResolution Refused(LocalizationKey localizationKey, string logReason) => new(false, null, localizationKey, logReason);
    }
}
