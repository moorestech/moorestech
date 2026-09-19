using Client.PlaytestReceiver.Http;

namespace Client.PlaytestReceiver.Upload.Attempt
{
    // 1試行のどの段で失敗したか。受け口の403とR2の403のように、同じ状態コードでも段で意味が変わる
    // Which stage of an attempt failed; the same status code means different things per stage, like a receiver 403 versus an R2 403
    internal enum PlaytestUploadStage
    {
        Prepare,

        // prepareは通ったが、宣言ファイルと長さ違いの同名キーがR2に既にあると受け口が返した
        // prepare passed, but the receiver reported a same-named key of another length already in R2 for a declared file
        PrepareConflict,

        SignedPut,
        Complete,

        // completeが欠けと数えたキーに、PUTが412（既にある）を返していた。段ではなくcompleteの結果とPUTの結果の食い違い
        // complete counted as missing a key whose PUT had answered 412 (already there); a conflict between complete's and the PUT's results rather than a stage
        StoredObjectMismatch,

        // 箱のファイルの読み書き（宣言の組み立て・印の記録・補足の読み込み）が例外で止まった
        // Reading or writing the box's files (building the declaration, recording markers, reading the supplement) stopped with an exception
        LocalFiles,
    }

    // 失敗した試行の段・対象パス・結果。生成は段ごとのファクトリだけに閉じる
    // A failed attempt's stage, target path and result; instances are made only through the per-stage factories
    internal sealed class PlaytestUploadAttemptFailure
    {
        public readonly PlaytestUploadStage Stage;
        public readonly PlaytestApiResult Result;

        // 対象のファイルパス（署名付きPUT・長さ違いのキー・食い違いのキー）。他の段では空
        // The target file path (a presigned PUT, a key of another length, a mismatched key); empty for the other stages
        public readonly string Path;

        // 段に固有の補足。ログ専用で、応答本文（署名付きURLを含みうる）の代わりに説明へ載せる
        // A stage-specific note; for logs only, described in place of the response body (which may hold presigned URLs)
        public readonly string Detail;

        private PlaytestUploadAttemptFailure(PlaytestUploadStage stage, string path, PlaytestApiResult result, string detail)
        {
            Stage = stage;
            Path = path;
            Result = result;
            Detail = detail;
        }

        public static PlaytestUploadAttemptFailure AtPrepare(PlaytestApiResult result)
        {
            return new PlaytestUploadAttemptFailure(PlaytestUploadStage.Prepare, "", result, "");
        }

        public static PlaytestUploadAttemptFailure PrepareConflictAt(string path, PlaytestApiResult prepareResult, string detail)
        {
            return new PlaytestUploadAttemptFailure(PlaytestUploadStage.PrepareConflict, path, prepareResult, detail);
        }

        public static PlaytestUploadAttemptFailure AtSignedPut(string path, PlaytestApiResult result)
        {
            return new PlaytestUploadAttemptFailure(PlaytestUploadStage.SignedPut, path, result, "");
        }

        public static PlaytestUploadAttemptFailure AtComplete(PlaytestApiResult result)
        {
            return new PlaytestUploadAttemptFailure(PlaytestUploadStage.Complete, "", result, "");
        }

        public static PlaytestUploadAttemptFailure StoredObjectMismatchAt(string path, PlaytestApiResult completeResult)
        {
            return new PlaytestUploadAttemptFailure(PlaytestUploadStage.StoredObjectMismatch, path, completeResult, "");
        }

        public static PlaytestUploadAttemptFailure AtLocalFiles(PlaytestApiResult localResult)
        {
            return new PlaytestUploadAttemptFailure(PlaytestUploadStage.LocalFiles, "", localResult, "");
        }
    }
}
