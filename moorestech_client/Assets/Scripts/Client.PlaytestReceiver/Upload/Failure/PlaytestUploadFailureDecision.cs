namespace Client.PlaytestReceiver.Upload.Failure
{
    internal enum PlaytestUploadFailureKind
    {
        // 権利の問題（取り直しても残る401・受け口の403）。箱を数える
        // A permission matter (a 401 surviving a refresh, a receiver 403); the box is counted
        Unauthorized,

        // 待てば直りうる。同一走行内で表の回数だけやり直し、尽きたら数えずに持ち越す
        // May heal with time; retried per the schedule within the run, then deferred uncounted
        Retryable,

        // 必須ファイルの拒否。一過性かもしれないので表の回数だけやり直し、尽きてなお拒まれたら箱を数える
        // A required file was refused; it may be transient, so it is retried per the schedule and the box is counted only if still refused after that
        RetryableThenPermanentForBox,

        // その箱は何度送っても直らない。箱を数える
        // That box never heals however often it is sent; the box is counted
        PermanentForBox,

        // 1ファイルだけが直らない（R2の拒否・長さ違いの既存キー・手元で変わったファイル）。そのファイルを見送りに落としてprepareからやり直す
        // A single file never heals (an R2 refusal, an existing key of another length, a file changed locally); it drops into the skips and the box restarts from prepare
        PermanentForFile,

        // セッションそのものが拒まれた（チケット拒否）。どの箱も同じ理由で通らないので、やり直さず数えず走行を止める
        // The session itself was refused (ticket rejected); every box fails alike, so the run stops without retrying or counting
        SessionRefused,
    }

    // 失敗1件の裁定。呼び出し側は Kind と AbortsRun と Description だけを読む
    // The verdict on one failure; callers read only Kind, AbortsRun and Description
    internal sealed class PlaytestUploadFailureDecision
    {
        public readonly PlaytestUploadFailureKind Kind;

        // 表が尽きたとき走行ごと打ち切るか（到達不能・トークン不調・バケット全体の失敗は残りの箱も同じ理由で失敗する）
        // Whether the run stops once the schedule runs out (unreachability, a token failure or a bucket-wide failure fails every remaining box alike)
        public readonly bool AbortsRun;
        public readonly string Description;

        public PlaytestUploadFailureDecision(PlaytestUploadFailureKind kind, bool abortsRun, string description)
        {
            Kind = kind;
            AbortsRun = abortsRun;
            Description = description;
        }
    }
}
