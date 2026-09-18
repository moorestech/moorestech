using System;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Http.Responses;
using Client.PlaytestReceiver.Upload.Attempt;

namespace Client.PlaytestReceiver.Upload.Failure
{
    // 失敗1件のログ・UPLOAD_ATTEMPTS用の説明。段と結果から1行を組む
    // The one-line description of a failure for logs and UPLOAD_ATTEMPTS, composed from the stage and the result
    internal static class PlaytestUploadFailureDescription
    {
        public static string Describe(PlaytestUploadAttemptFailure failure)
        {
            var what = DescribeStage(failure);
            var result = failure.Result;
            if (result.Kind != PlaytestApiResultKind.Responded) return $"{what}: {result.Kind} {result.Detail}";
            // R2のエラー本文は署名の検算材料を含みうるので、Code と Message だけを残す
            // R2's error body may carry the signature's verification material, so only Code and Message are kept
            if (failure.Stage == PlaytestUploadStage.SignedPut && R2ErrorBody.TryRead(result.Body, out var code, out var message)) return $"{what}: HTTP {result.StatusCode} {code}: {message}";
            return $"{what}: HTTP {result.StatusCode} {result.Body}";
        }

        private static string DescribeStage(PlaytestUploadAttemptFailure failure)
        {
            switch (failure.Stage)
            {
                case PlaytestUploadStage.Prepare: return "prepare";
                case PlaytestUploadStage.SignedPut: return $"PUT {failure.Path}";
                case PlaytestUploadStage.Complete: return "complete";
                case PlaytestUploadStage.StoredObjectMismatch: return $"R2 already holds {failure.Path} (PUT answered 412) but complete counts it missing, so it differs from the declaration and cannot be overwritten";
                default: throw new ArgumentOutOfRangeException(nameof(failure), failure.Stage, "unknown upload stage");
            }
        }
    }
}
