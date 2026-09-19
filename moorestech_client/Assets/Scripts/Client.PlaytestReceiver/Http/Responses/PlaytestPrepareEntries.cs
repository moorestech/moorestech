namespace Client.PlaytestReceiver.Http.Responses
{
    public enum PlaytestPrepareOutcome
    {
        Prepared,
        AlreadyAcked,
    }

    // 署名付きURLを受け取った宣言ファイル1件。URLは絶対https、長さは宣言どおりであることを検査済み
    // One declared file that received a presigned URL; the URL is checked to be absolute https and the length to match the declaration
    public sealed class PlaytestPreparedUpload
    {
        public readonly PlaytestDeclaredFile File;
        public readonly string Url;

        public PlaytestPreparedUpload(PlaytestDeclaredFile file, string url)
        {
            File = file;
            Url = url;
        }
    }

    // R2に宣言と長さの違う同名キーが既にある宣言ファイル1件。If-None-Match: * のPUTでは上書きできない
    // One declared file whose key already exists in R2 with a different length; a PUT under If-None-Match: * can never overwrite it
    public sealed class PlaytestPrepareConflict
    {
        public readonly PlaytestDeclaredFile File;
        public readonly long ActualBytes;

        public PlaytestPrepareConflict(PlaytestDeclaredFile file, long actualBytes)
        {
            File = file;
            ActualBytes = actualBytes;
        }
    }
}
