using System;
using Client.PlaytestReceiver.Http;

namespace Client.PlaytestReceiver.Upload
{
    internal enum PlaytestUploadFailureKind
    {
        Unauthorized,
        Retryable,
        PermanentForFile,
    }

    // 失敗の読み分けの唯一の判定地点。「権利の問題」「一時的」「そのファイルは何度送っても直らない」を分ける
    // The single place that sorts failures into a permission matter, a transient one, or one that never heals for that file
    internal static class PlaytestUploadFailurePolicy
    {
        public static PlaytestUploadFailureKind Classify(PlaytestApiResult result)
        {
            switch (result.Kind)
            {
                case PlaytestApiResultKind.LocalUnsafePath:
                    return PlaytestUploadFailureKind.PermanentForFile;
                case PlaytestApiResultKind.LocalUnreadableFile:
                case PlaytestApiResultKind.TransportFailure:
                case PlaytestApiResultKind.SessionUnavailable:
                    return PlaytestUploadFailureKind.Retryable;
                case PlaytestApiResultKind.Responded:
                    return ClassifyStatusCode(result.StatusCode);
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result.Kind, "unknown api result kind");
            }

            #region Internal

            // 取り直しても残る401と403は権利の問題、408/429/5xxは混み合いや障害、それ以外の4xxはそのファイル固有
            // A 401 surviving a refresh or a 403 is a permission matter, 408/429/5xx is congestion or outage, other 4xx belong to the file
            PlaytestUploadFailureKind ClassifyStatusCode(int statusCode)
            {
                if (statusCode == 401 || statusCode == 403) return PlaytestUploadFailureKind.Unauthorized;
                if (statusCode == 408 || statusCode == 429) return PlaytestUploadFailureKind.Retryable;
                if (400 <= statusCode && statusCode < 500) return PlaytestUploadFailureKind.PermanentForFile;
                return PlaytestUploadFailureKind.Retryable;
            }

            #endregion
        }

        // 到達できない・トークンが取れない失敗は、残りの箱も同じ理由で失敗する。走行ごと打ち切る
        // Unreachability or an unobtainable token fails every remaining box the same way, so the whole run stops
        public static bool AbortsRun(PlaytestApiResult result)
        {
            return result.Kind == PlaytestApiResultKind.TransportFailure || result.Kind == PlaytestApiResultKind.SessionUnavailable;
        }

        // 要約JSONのskipped[]へ載せる短い理由。人が読む詳細は本文ではなくログへ出す
        // The short reason that rides in the summary's skipped[]; the human-readable detail goes to the log, not the body
        public static string ToSkipReason(PlaytestApiResult result)
        {
            return result.Kind == PlaytestApiResultKind.LocalUnsafePath ? "unsafe-path" : $"http-{result.StatusCode}";
        }

        public static string Describe(string what, PlaytestApiResult result)
        {
            return result.Kind == PlaytestApiResultKind.Responded ? $"{what}: HTTP {result.StatusCode} {result.Body}" : $"{what}: {result.Kind} {result.Detail}";
        }
    }
}
