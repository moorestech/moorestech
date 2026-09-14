using System;
using UnityEngine;

namespace Client.PlaytestReceiver
{
    // 受け口の接続先/時間の定数。baseのみ環境変数で差替可
    // Connection and timing constants for the receiver; only the base URL can be overridden by env for verification machines
    public static class PlaytestReceiverConfig
    {
        private const string DefaultBaseUrl = "https://playtest.tar-atari.com";
        public const string SteamIdentity = "moorestech-playtest";
        private const string BaseUrlEnvironmentVariable = "MOORESTECH_PLAYTEST_RECEIVER_BASE";
        public const int TicketTimeoutSeconds = 15;
        public const int HttpTimeoutSeconds = 60;

        // 受け口のトークン寿命は3600秒。45分で取り直し、送信中に切れる窓を作らない
        // The receiver's token lives 3600s; refreshing at 45 min leaves no window where it expires mid-upload
        public const int TokenRefreshAfterSeconds = 2700;
        public const long MaxFileBytes = 100L * 1024 * 1024;

        // アップロードの期限は本文の送信時間を含む。遅い回線の大きい箱を殺さないよう、サイズに比例した猶予を足す
        // An upload deadline covers the body too, so slow lines get extra time proportional to the file size
        public const int UploadBytesPerSecondBudget = 128 * 1024;
        public const int MaxUploadTimeoutSeconds = 900;

        public static TimeSpan UploadTimeout(long fileBytes)
        {
            var seconds = HttpTimeoutSeconds + fileBytes / UploadBytesPerSecondBudget;
            return TimeSpan.FromSeconds(Math.Min(seconds, MaxUploadTimeoutSeconds));
        }

        public static string BaseUrl
        {
            get
            {
                var overridden = Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable);
                if (string.IsNullOrEmpty(overridden)) return DefaultBaseUrl;
                Debug.Log($"[PlaytestReceiver] base URL overridden by {BaseUrlEnvironmentVariable}: {overridden}");
                return overridden;
            }
        }
    }
}
