using System;
using UnityEngine;

namespace Client.PlaytestReceiver
{
    // 受け口の接続先/時間の定数。受け口と共有する値は contract.json との一致をテストで固定する
    // Connection and timing constants for the receiver; values shared with the receiver are pinned to contract.json by a test
    public static class PlaytestReceiverConfig
    {
        private const string DefaultBaseUrl = "https://playtest.tar-atari.com";
        public const string SteamIdentity = "moorestech-playtest";
        public const int TicketTimeoutSeconds = 15;
        public const int HttpTimeoutSeconds = 60;

        // 受け口が名乗った期限のこの秒数前に取り直す。送信中に切れる窓と端末の時計ずれを吸収する
        // Refresh this many seconds before the expiry the receiver states, absorbing mid-upload expiry and clock skew
        public const int TokenRefreshMarginSeconds = 900;
        public const long MaxFileBytes = 100L * 1024 * 1024;

        // 署名付きURLへのPUTは経過時間で切らず、バイトが進まない時間で切る（低速回線を殺さない。ADR 0064）
        // A PUT to the presigned URL is cut by stalled bytes, not elapsed time, so slow lines survive (ADR 0064)
        public const int UploadIdleTimeoutSeconds = 60;
        public const int UploadUrlTtlSeconds = 3600;
        public const int MaxBundleFiles = 128;
        public const long MaxBundleBytes = 256L * 1024 * 1024;

        // 受け口が予約する先頭セグメント名（印と操作名）。送信前にこの一覧で見送る
        // First segment names the receiver reserves (markers and verbs); files under them are skipped before sending
        internal static readonly string[] ReservedUploadSegments = { "READY", "ACKED", "DECLARED", "complete", "prepare" };

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
