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
        // isSignedPut は署名付きURL（R2）へのPUTの結果か。受け口の403とR2の403は意味が違う
        // isSignedPut tells whether the result came from the presigned PUT to R2; a receiver 403 and an R2 403 mean different things
        public static PlaytestUploadFailureKind Classify(PlaytestApiResult result, bool isSignedPut)
        {
            switch (result.Kind)
            {
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

            // 取り直しても残る401と受け口の403は権利の問題、408/409/429/5xxは一過性、それ以外の4xxはその箱固有
            // A 401 surviving a refresh or a receiver 403 is a permission matter, 408/409/429/5xx is transient, other 4xx belong to the box
            PlaytestUploadFailureKind ClassifyStatusCode(int statusCode)
            {
                if (statusCode == 401 || (statusCode == 403 && !isSignedPut)) return PlaytestUploadFailureKind.Unauthorized;
                // 署名付きURLへのPUTの403は署名の期限切れか長さ不一致。prepareからやり直せば直るので一過性として扱う
                // A 403 from the presigned PUT means an expired signature or a length mismatch; redoing from prepare heals it, so it is transient
                if (statusCode == 403) return PlaytestUploadFailureKind.Retryable;
                // 409はcompleteの「揃っていない」「prepareが無い」。prepareからやり直せば直る
                // A 409 is complete's "incomplete" or "not-prepared"; redoing from prepare heals it
                if (statusCode == 408 || statusCode == 409 || statusCode == 429) return PlaytestUploadFailureKind.Retryable;
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

        public static string Describe(string what, PlaytestApiResult result)
        {
            return result.Kind == PlaytestApiResultKind.Responded ? $"{what}: HTTP {result.StatusCode} {result.Body}" : $"{what}: {result.Kind} {result.Detail}";
        }
    }
}
