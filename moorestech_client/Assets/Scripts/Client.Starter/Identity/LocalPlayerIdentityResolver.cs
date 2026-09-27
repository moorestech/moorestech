using System;
using System.Security.Cryptography;
using System.Text;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.PlaytestReceiver.Steam;
using UnityEngine;

namespace Client.Starter.Identity
{
    // 起動ごとのプレイヤー身元を決める。Steam配布ビルドはSteam、それ以外は端末値（ADR 0073）
    // Resolves this process's player identity: Steam on Steam distribution builds, the device value otherwise (ADR 0073)
    public static class LocalPlayerIdentityResolver
    {
        public const string SteamUnavailableKey = "ui.loading.steamIdentityUnavailable";
        public const string DeviceUnavailableKey = "ui.loading.deviceIdentityUnavailable";

        public static PlayerIdentityResolution ResolveForThisProcess()
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
                if (steamReader.TryRead(out var steamId, out var failureReason)) return PlayerIdentityResolution.Success("steam:" + steamId);
                return PlayerIdentityResolution.Refused(SteamUnavailableKey, $"Steam配布ビルドでSteamIDを読めないため開始しない: {failureReason}");
            }

            if (string.IsNullOrEmpty(deviceUniqueIdentifier) || deviceUniqueIdentifier == SystemInfo.unsupportedIdentifier)
            {
                return PlayerIdentityResolution.Refused(DeviceUnavailableKey, $"端末の識別子を取得できないため開始しない: '{deviceUniqueIdentifier}'");
            }
            return PlayerIdentityResolution.Success("device:" + Sha256Hex(deviceUniqueIdentifier));

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
        public readonly string RefusalLocalizationKey;
        public readonly string RefusalLogReason;

        private PlayerIdentityResolution(bool succeeded, string identity, string refusalLocalizationKey, string refusalLogReason)
        {
            Succeeded = succeeded;
            Identity = identity;
            RefusalLocalizationKey = refusalLocalizationKey;
            RefusalLogReason = refusalLogReason;
        }

        public static PlayerIdentityResolution Success(string identity) => new(true, identity, null, null);
        public static PlayerIdentityResolution Refused(string localizationKey, string logReason) => new(false, null, localizationKey, logReason);
    }
}
