using System;
using Client.PlaytestReceiver.Http;

namespace Client.PlaytestReceiver.Upload
{
    // 失敗の読み分け。「取り直せば直る」「そのファイルは何度送っても直らない」「後で再試行」の唯一の判定地点
    // The single place that tells a refreshable failure from one that never heals for that file, or a plain retry-later
    public static class PlaytestUploadFailurePolicy
    {
        // 401は期限切れ、403は権限の取り消し、408と429は混み合い。いずれも箱の事情なのでファイルを見送らない
        // 401 is expiry, 403 a revoked permission, 408 and 429 congestion; all are box-level, so no file is dropped for them
        private static readonly int[] BoxLevelStatusCodes = { 401, 403, 408, 429 };

        public static bool IsUnauthorized(PlaytestApiResult result)
        {
            return !result.IsTransportFailure && result.StatusCode == 401;
        }

        public static bool IsPermanentForFile(PlaytestApiResult result)
        {
            if (result.IsTransportFailure) return false;
            if (Array.IndexOf(BoxLevelStatusCodes, result.StatusCode) >= 0) return false;
            return 400 <= result.StatusCode && result.StatusCode < 500;
        }

        // 要約JSONのskipped[]へ載せる短い理由。人が読む詳細は本文ではなくログへ出す
        // The short reason that rides in the summary's skipped[]; the human-readable detail goes to the log, not the body
        public static string ToSkipReason(PlaytestApiResult result)
        {
            return $"http-{result.StatusCode}";
        }

        public static string Describe(string what, PlaytestApiResult result)
        {
            return result.IsTransportFailure ? $"{what}: {result.TransportError}" : $"{what}: HTTP {result.StatusCode} {result.Body}";
        }
    }
}
