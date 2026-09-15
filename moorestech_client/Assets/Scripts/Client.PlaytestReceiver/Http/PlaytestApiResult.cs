namespace Client.PlaytestReceiver.Http
{
    // 受け口が応答したのか、送る前にこちらで止めたのかの区別。偽の状態コードで表さない
    // Tells a receiver response apart from a refusal made here before sending, without forging status codes
    public enum PlaytestApiResultKind
    {
        Responded,
        TransportFailure,
        LocalUnsafePath,
        LocalUnreadableFile,
        SessionUnavailable,
    }

    // HTTPの結末。生成は種別ごとのファクトリだけに閉じる
    // The outcome of one HTTP call; instances are made only through the per-kind factories
    public sealed class PlaytestApiResult
    {
        public readonly PlaytestApiResultKind Kind;
        public readonly int StatusCode;
        public readonly string Body;

        // 応答以外の結末の理由。ログ専用
        // Why a non-response ended the call; for logs only
        public readonly string Detail;

        private PlaytestApiResult(PlaytestApiResultKind kind, int statusCode, string body, string detail)
        {
            Kind = kind;
            StatusCode = statusCode;
            Body = body;
            Detail = detail;
        }

        public static PlaytestApiResult Responded(int statusCode, string body)
        {
            return new PlaytestApiResult(PlaytestApiResultKind.Responded, statusCode, body, "");
        }

        public static PlaytestApiResult TransportFailure(string detail)
        {
            return new PlaytestApiResult(PlaytestApiResultKind.TransportFailure, 0, "", detail);
        }

        public static PlaytestApiResult LocalUnsafePath(string detail)
        {
            return new PlaytestApiResult(PlaytestApiResultKind.LocalUnsafePath, 0, "", detail);
        }

        public static PlaytestApiResult LocalUnreadableFile(string detail)
        {
            return new PlaytestApiResult(PlaytestApiResultKind.LocalUnreadableFile, 0, "", detail);
        }

        public static PlaytestApiResult SessionUnavailable(string detail)
        {
            return new PlaytestApiResult(PlaytestApiResultKind.SessionUnavailable, 0, "", detail);
        }

        public bool IsSuccess => Kind == PlaytestApiResultKind.Responded && 200 <= StatusCode && StatusCode < 300;
    }
}
