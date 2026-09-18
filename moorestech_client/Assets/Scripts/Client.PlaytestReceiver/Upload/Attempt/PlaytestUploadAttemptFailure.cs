using Client.PlaytestReceiver.Http;

namespace Client.PlaytestReceiver.Upload.Attempt
{
    // 1試行のどの段で失敗したか。受け口の403とR2の403のように、同じ状態コードでも段で意味が変わる
    // Which stage of an attempt failed; the same status code means different things per stage, like a receiver 403 versus an R2 403
    internal enum PlaytestUploadStage
    {
        Prepare,
        SignedPut,
        Complete,

        // completeが欠けと数えたキーに、PUTが412（既にある）を返していた。段ではなくcompleteの結果とPUTの結果の食い違い
        // complete counted as missing a key whose PUT had answered 412 (already there); a conflict between complete's and the PUT's results rather than a stage
        StoredObjectMismatch,
    }

    // 失敗した試行の段・対象パス・結果。生成は段ごとのファクトリだけに閉じる
    // A failed attempt's stage, target path and result; instances are made only through the per-stage factories
    internal sealed class PlaytestUploadAttemptFailure
    {
        public readonly PlaytestUploadStage Stage;
        public readonly PlaytestApiResult Result;

        // 署名付きPUTの対象パス（食い違いではそのキー）。他の段では空
        // The target path of the presigned PUT (the key, for a mismatch); empty for the other stages
        public readonly string Path;

        private PlaytestUploadAttemptFailure(PlaytestUploadStage stage, string path, PlaytestApiResult result)
        {
            Stage = stage;
            Path = path;
            Result = result;
        }

        public static PlaytestUploadAttemptFailure AtPrepare(PlaytestApiResult result)
        {
            return new PlaytestUploadAttemptFailure(PlaytestUploadStage.Prepare, "", result);
        }

        public static PlaytestUploadAttemptFailure AtSignedPut(string path, PlaytestApiResult result)
        {
            return new PlaytestUploadAttemptFailure(PlaytestUploadStage.SignedPut, path, result);
        }

        public static PlaytestUploadAttemptFailure AtComplete(PlaytestApiResult result)
        {
            return new PlaytestUploadAttemptFailure(PlaytestUploadStage.Complete, "", result);
        }

        public static PlaytestUploadAttemptFailure StoredObjectMismatchAt(string path, PlaytestApiResult completeResult)
        {
            return new PlaytestUploadAttemptFailure(PlaytestUploadStage.StoredObjectMismatch, path, completeResult);
        }
    }
}
