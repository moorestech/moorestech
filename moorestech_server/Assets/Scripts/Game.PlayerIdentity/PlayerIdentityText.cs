using System;
using System.Security.Cryptography;
using System.Text;

namespace Game.PlayerIdentity
{
    // プレイヤー身元の文字列書式。種別の接頭辞で出どころを区別する
    // The player identity text format; the prefix tells where it came from
    public static class PlayerIdentityText
    {
        public const string SteamPrefix = "steam:";
        public const string DevicePrefix = "device:";
        private const int MaxSteamIdDigits = 20;
        private const int DeviceHashLength = 64;

        // 端末身元の作り方の正本。生の端末識別子をサーバーやセーブへ出さないためハッシュ化する
        // The single source for building a device identity; hashed so the raw device identifier never reaches a server or a save
        public static string ForDevice(string deviceUniqueIdentifier)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(deviceUniqueIdentifier));
            return DevicePrefix + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static bool IsValid(string identity, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(identity))
            {
                reason = "身元が空";
                return false;
            }

            // Steamは10進のSteamID64、端末はSHA-256の小文字16進
            // Steam carries a decimal SteamID64; a device carries a lowercase hex SHA-256
            if (identity.StartsWith(SteamPrefix, StringComparison.Ordinal)) return IsSteamBody(identity.Substring(SteamPrefix.Length), out reason);
            if (identity.StartsWith(DevicePrefix, StringComparison.Ordinal)) return IsDeviceBody(identity.Substring(DevicePrefix.Length), out reason);

            reason = $"未知の身元種別: {identity}";
            return false;

            #region Internal

            bool IsSteamBody(string body, out string steamReason)
            {
                steamReason = null;
                if (body.Length == 0 || MaxSteamIdDigits < body.Length)
                {
                    steamReason = $"SteamIDの桁数が不正: {identity}";
                    return false;
                }
                foreach (var c in body)
                {
                    if ('0' <= c && c <= '9') continue;
                    steamReason = $"SteamIDに数字以外が含まれる: {identity}";
                    return false;
                }
                return steamReason == null;
            }

            bool IsDeviceBody(string body, out string deviceReason)
            {
                deviceReason = null;
                if (body.Length != DeviceHashLength) deviceReason = $"端末値の長さが不正: {identity}";
                foreach (var c in body)
                {
                    if (('0' <= c && c <= '9') || ('a' <= c && c <= 'f')) continue;
                    deviceReason = $"端末値に小文字16進以外が含まれる: {identity}";
                }
                return deviceReason == null;
            }

            #endregion
        }
    }
}
