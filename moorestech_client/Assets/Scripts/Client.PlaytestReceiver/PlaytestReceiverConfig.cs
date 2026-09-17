using System;
using UnityEngine;

namespace Client.PlaytestReceiver
{
    // 受け口の接続先/時間の定数。受け口と共有する値は contract.json との一致をテストで固定する
    // Connection and timing constants for the receiver; values shared with the receiver are pinned to contract.json by a test
    public static class PlaytestReceiverConfig
    {
        private const string DefaultBaseUrl = "https://playtest.moores.tech";
        public const string SteamIdentity = "moorestech-playtest";
        public const int TicketTimeoutSeconds = 15;
        public const int HttpTimeoutSeconds = 60;

        // 受け口が名乗った期限のこの秒数前に取り直す。送信中に切れる窓と端末の時計ずれを吸収する
        // Refresh this many seconds before the expiry the receiver states, absorbing mid-upload expiry and clock skew
        public const int TokenRefreshMarginSeconds = 900;
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

        // 配布版は定数に固定する。差し替えを許すと偽の受け口で照合そのものを無効化できる
        // Release builds are pinned to the constant; allowing an override would let a fake receiver disable the check
        public static string BaseUrl
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                const string baseUrlEnvironmentVariable = "MOORESTECH_PLAYTEST_RECEIVER_BASE";
                var overridden = Environment.GetEnvironmentVariable(baseUrlEnvironmentVariable);
                if (!string.IsNullOrEmpty(overridden))
                {
                    Debug.Log($"[PlaytestReceiver] base URL overridden by {baseUrlEnvironmentVariable}: {overridden}");
                    return overridden;
                }
#endif
                return DefaultBaseUrl;
            }
        }
    }
}
