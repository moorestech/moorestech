namespace Client.PlaytestReceiver.Http
{
    // 受け口が応答したのか、送る前にこちらで止めたのかの区別。偽の状態コードで表さない
    // Tells a receiver response apart from a refusal made here before sending, without forging status codes
    public enum PlaytestApiResultKind
    {
        Responded,
        TransportFailure,

        // 手元のファイルが消えた・宣言の長さから変わった。やり直しても戻らない
        // The local file vanished or no longer has its declared length; a retry never brings it back
        LocalFileChanged,

        // 手元のファイルが一時的に読めない（他プロセスのロック・一時的なI/O障害）。待てば直りうる
        // The local file is temporarily unreadable (a foreign lock, a transient I/O fault); it may heal with time
        LocalFileUnavailable,

        SessionUnavailable,

        // 受け口は2xxで応答したが中身が契約の形でない（版ずれ・キャプティブポータル等）。到達失敗と混ぜない
        // The receiver answered 2xx but the body breaks the contract (version skew, captive portal); kept apart from unreachability
        MalformedResponse,
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

        // トークンが取れなかった理由。SessionUnavailable でだけ意味を持ち、他の種別では Authenticated
        // Why no token was available; meaningful only for SessionUnavailable, Authenticated for every other kind
        public readonly PlaytestSessionOutcome SessionOutcome;

        private PlaytestApiResult(PlaytestApiResultKind kind, int statusCode, string body, string detail, PlaytestSessionOutcome sessionOutcome)
        {
            Kind = kind;
            StatusCode = statusCode;
            Body = body;
            Detail = detail;
            SessionOutcome = sessionOutcome;
        }

        public static PlaytestApiResult Responded(int statusCode, string body)
        {
            return new PlaytestApiResult(PlaytestApiResultKind.Responded, statusCode, body, "", PlaytestSessionOutcome.Authenticated);
        }

        public static PlaytestApiResult TransportFailure(string detail)
        {
            return new PlaytestApiResult(PlaytestApiResultKind.TransportFailure, 0, "", detail, PlaytestSessionOutcome.Authenticated);
        }

        public static PlaytestApiResult LocalFileChanged(string detail)
        {
            return new PlaytestApiResult(PlaytestApiResultKind.LocalFileChanged, 0, "", detail, PlaytestSessionOutcome.Authenticated);
        }

        public static PlaytestApiResult LocalFileUnavailable(string detail)
        {
            return new PlaytestApiResult(PlaytestApiResultKind.LocalFileUnavailable, 0, "", detail, PlaytestSessionOutcome.Authenticated);
        }

        public static PlaytestApiResult SessionUnavailable(PlaytestSessionOutcome sessionOutcome, string detail)
        {
            return new PlaytestApiResult(PlaytestApiResultKind.SessionUnavailable, 0, "", detail, sessionOutcome);
        }

        public static PlaytestApiResult MalformedResponse(int statusCode, string detail)
        {
            return new PlaytestApiResult(PlaytestApiResultKind.MalformedResponse, statusCode, "", detail, PlaytestSessionOutcome.Authenticated);
        }

        public bool IsSuccess => Kind == PlaytestApiResultKind.Responded && 200 <= StatusCode && StatusCode < 300;
    }
}
