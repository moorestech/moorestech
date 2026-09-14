using System;
using UnityEngine;

namespace Client.PlaytestReceiver
{
    // 受け口の接続先と時間の定数。検証機・ステージング用に環境変数でbaseだけ差し替えられる
    // Connection and timing constants for the receiver; only the base URL can be overridden by env for verification machines
    public static class PlaytestReceiverConfig
    {
        public const string DefaultBaseUrl = "https://playtest.tar-atari.com";
        public const string SteamIdentity = "moorestech-playtest";
        public const string BaseUrlEnvironmentVariable = "MOORESTECH_PLAYTEST_RECEIVER_BASE";
        public const int TicketTimeoutSeconds = 15;
        public const int HttpTimeoutSeconds = 60;

        // 受け口のトークン寿命は3600秒。45分で取り直し、送信中に切れる窓を作らない
        // The receiver's token lives 3600s; refreshing at 45 min leaves no window where it expires mid-upload
        public const int TokenRefreshAfterSeconds = 2700;
        public const long MaxFileBytes = 100L * 1024 * 1024;

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
