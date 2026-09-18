using System;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Http.Responses;
using Client.PlaytestReceiver.Upload.Attempt;

namespace Client.PlaytestReceiver.Upload.Failure
{
    // 失敗1件のログ・UPLOAD_ATTEMPTS・見送り記録用の説明。段と結果から1行を組む
    // The one-line description of a failure for logs, UPLOAD_ATTEMPTS and the skip record, composed from the stage and the result
    internal static class PlaytestUploadFailureDescription
    {
        public static string Describe(PlaytestUploadAttemptFailure failure)
        {
            var what = DescribeStage();
            var result = failure.Result;
            // 長さ違いのキーは prepare 応答本文（署名付きURLを含む）を出さず、段の補足だけを残す
            // A key of another length leaves only the stage note, never the prepare body (which holds presigned URLs)
            if (failure.Stage == PlaytestUploadStage.PrepareConflict) return $"{what}: {failure.Detail}";
            if (result.Kind != PlaytestApiResultKind.Responded) return $"{what}: {result.Kind} {result.Detail}";
            // R2のエラー本文は署名の検算材料を含みうるので、Code と Message だけを残す
            // R2's error body may carry the signature's verification material, so only Code and Message are kept
            if (failure.Stage == PlaytestUploadStage.SignedPut && R2ErrorBody.TryRead(result.Body, out var code, out var message)) return $"{what}: HTTP {result.StatusCode} {code}: {message}";
            return $"{what}: HTTP {result.StatusCode} {result.Body}";

            #region Internal

            string DescribeStage()
            {
                switch (failure.Stage)
                {
                    case PlaytestUploadStage.Prepare: return "prepare";
                    case PlaytestUploadStage.PrepareConflict: return $"prepare conflict on {failure.Path}";
                    case PlaytestUploadStage.SignedPut: return $"PUT {failure.Path}";
                    case PlaytestUploadStage.Complete: return "complete";
                    case PlaytestUploadStage.StoredObjectMismatch: return $"R2 already holds {failure.Path} (PUT answered 412) but complete counts it missing, so it differs from the declaration and cannot be overwritten";
                    case PlaytestUploadStage.LocalFiles: return "box files";
                    default: throw new ArgumentOutOfRangeException(nameof(failure), failure.Stage, "unknown upload stage");
                }
            }

            #endregion
        }

        // 見送ったファイルの理由語。complete補足の skipped[] と UPLOAD_SKIPPED に載る
        // The reason word for a skipped file; goes into the complete supplement's skipped[] and UPLOAD_SKIPPED
        public static string SkipReasonOf(PlaytestUploadAttemptFailure failure)
        {
            if (failure.Stage == PlaytestUploadStage.PrepareConflict) return "stored-length-mismatch";
            if (failure.Result.Kind == PlaytestApiResultKind.LocalFileChanged) return "local-file-changed";
            return $"http-{failure.Result.StatusCode}";
        }
    }
}
