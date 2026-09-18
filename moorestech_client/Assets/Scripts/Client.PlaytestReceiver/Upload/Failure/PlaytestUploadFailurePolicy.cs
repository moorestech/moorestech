using System;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Http.Responses;
using Client.PlaytestReceiver.Upload.Attempt;

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

        // その箱は何度送っても直らない。箱を数える
        // That box never heals however often it is sent; the box is counted
        PermanentForBox,

        // 署名付きPUTで1ファイルだけが拒まれた。そのファイルを見送りに落としてprepareからやり直す
        // The presigned PUT refused a single file; that file drops into the skips and the box restarts from prepare
        PermanentForFile,
    }

    // 失敗1件の裁定。呼び出し側は Kind と AbortsRun と Description だけを読む
    // The verdict on one failure; callers read only Kind, AbortsRun and Description
    internal sealed class PlaytestUploadFailureDecision
    {
        public readonly PlaytestUploadFailureKind Kind;

        // 表が尽きたとき走行ごと打ち切るか（到達不能・トークン不調は残りの箱も同じ理由で失敗する）
        // Whether the run stops once the schedule runs out (unreachability or a token failure fails every remaining box alike)
        public readonly bool AbortsRun;
        public readonly string Description;

        public PlaytestUploadFailureDecision(PlaytestUploadFailureKind kind, bool abortsRun, string description)
        {
            Kind = kind;
            AbortsRun = abortsRun;
            Description = description;
        }
    }

    // 失敗の読み分けの唯一の判定地点。段（prepare・署名付きPUT・complete）と結果から種別を決める
    // The single place that sorts failures; the kind is decided from the stage (prepare, presigned PUT, complete) and the result
    internal static class PlaytestUploadFailurePolicy
    {
        public static PlaytestUploadFailureDecision Decide(PlaytestUploadAttemptFailure failure)
        {
            var result = failure.Result;
            var description = PlaytestUploadFailureDescription.Describe(failure);
            switch (result.Kind)
            {
                case PlaytestApiResultKind.TransportFailure:
                case PlaytestApiResultKind.SessionUnavailable:
                    return new PlaytestUploadFailureDecision(PlaytestUploadFailureKind.Retryable, true, description);
                // 受け口は応答しているので、後続の箱は通りうる。その箱だけ持ち越す
                // The receiver is answering, so later boxes may pass; only this box is deferred
                case PlaytestApiResultKind.MalformedResponse:
                    return new PlaytestUploadFailureDecision(PlaytestUploadFailureKind.Retryable, false, description);
                // 宣言は走行の開始時に固定される。消えた・縮んだ・読めないファイルはやり直しても戻らない
                // The declaration is fixed when the run starts; a vanished, shrunk or unreadable file never comes back on retry
                case PlaytestApiResultKind.LocalUnreadableFile:
                    return new PlaytestUploadFailureDecision(PlaytestUploadFailureKind.PermanentForBox, false, description);
                case PlaytestApiResultKind.Responded:
                    var kind = failure.Stage == PlaytestUploadStage.SignedPut ? ClassifySignedPut(result) : ClassifyReceiver(result);
                    return new PlaytestUploadFailureDecision(kind, false, description);
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure), result.Kind, "unknown api result kind");
            }
        }

        // 受け口（prepare・complete）の応答。取り直しても残る401と403は権利の問題、宣言の衝突はその箱固有、408/409/429/5xxは一過性
        // A receiver answer (prepare, complete): a surviving 401 or a 403 is a permission matter, a declaration conflict belongs to the box, 408/409/429/5xx are transient
        private static PlaytestUploadFailureKind ClassifyReceiver(PlaytestApiResult result)
        {
            var statusCode = result.StatusCode;
            if (statusCode == 401 || statusCode == 403) return PlaytestUploadFailureKind.Unauthorized;
            // 宣言はwrite-once（縮小のみ可）。ファイルが増えた・長さが変わった箱は何度送っても同じ409になる
            // The declaration is write-once (shrinking only); a box whose files grew or changed length gets the same 409 forever
            if (statusCode == 409 && PlaytestReceiverResponseBody.ReadReason(result.Body) == "declaration-conflict") return PlaytestUploadFailureKind.PermanentForBox;
            // それ以外の409はcompleteの「揃っていない」「prepareが無い」。prepareからやり直せば直る
            // Any other 409 is complete's "incomplete" or "not-prepared"; redoing from prepare heals it
            if (statusCode == 408 || statusCode == 409 || statusCode == 429) return PlaytestUploadFailureKind.Retryable;
            if (400 <= statusCode && statusCode < 500) return PlaytestUploadFailureKind.PermanentForBox;
            return PlaytestUploadFailureKind.Retryable;
        }

        // 署名付きURL（R2）へのPUTの応答。403は理由で分け、それ以外の4xxはそのファイルだけの問題として見送る
        // An answer to the presigned PUT (R2): a 403 is split by its reason, and any other 4xx is that file's own problem and gets skipped
        private static PlaytestUploadFailureKind ClassifySignedPut(PlaytestApiResult result)
        {
            var statusCode = result.StatusCode;
            if (statusCode == 403) return PlaytestSignedPutForbidden.IsExpiredSignature(result.Body) ? PlaytestUploadFailureKind.Retryable : PlaytestUploadFailureKind.PermanentForBox;
            if (statusCode == 408 || statusCode == 429) return PlaytestUploadFailureKind.Retryable;
            if (400 <= statusCode && statusCode < 500) return PlaytestUploadFailureKind.PermanentForFile;
            return PlaytestUploadFailureKind.Retryable;
        }
    }
}
