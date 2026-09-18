using System;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Http.Responses;
using Client.PlaytestReceiver.Upload.Attempt;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload.Failure
{
    // 失敗の読み分けの唯一の判定地点。段（prepare・署名付きPUT・complete・箱のファイル）と結果、箱の格付けから種別を決める
    // The single place that sorts failures; the kind is decided from the stage (prepare, presigned PUT, complete, box files), the result and the box's ranking
    internal static class PlaytestUploadFailurePolicy
    {
        public static PlaytestUploadFailureDecision Decide(PlaytestUploadAttemptFailure failure, IPlaytestBoxFilePolicy filePolicy)
        {
            var result = failure.Result;
            var description = PlaytestUploadFailureDescription.Describe(failure);
            // 412で送信済みとしたキーがcompleteで欠けた＝R2に長さ違いの別物がある。上書きできないので何度送っても直らない
            // A key taken as sent on a 412 came back missing from complete: R2 holds another length there, which can never be overwritten
            if (failure.Stage == PlaytestUploadStage.StoredObjectMismatch) return Verdict(PlaytestUploadFailureKind.PermanentForBox, false);
            // prepareが長さ違いの既存キーを返した。上書きできないので、そのファイルか（必須なら）箱が直らない
            // prepare reported an existing key of another length; it can never be overwritten, so the file (or, if required, the box) never heals
            if (failure.Stage == PlaytestUploadStage.PrepareConflict) return Verdict(FileOrBox(PlaytestUploadFailureKind.PermanentForBox), false);
            switch (result.Kind)
            {
                case PlaytestApiResultKind.TransportFailure:
                    return Verdict(PlaytestUploadFailureKind.Retryable, true);
                case PlaytestApiResultKind.SessionUnavailable:
                    return DecideSession();
                // 受け口は応答しているので、後続の箱は通りうる。その箱だけ持ち越す
                // The receiver is answering, so later boxes may pass; only this box is deferred
                case PlaytestApiResultKind.MalformedResponse:
                    return Verdict(PlaytestUploadFailureKind.Retryable, false);
                // ロックや一時的なI/O障害は待てば直りうる。表の回数だけやり直す
                // A lock or transient I/O fault may heal with time; retried per the schedule
                case PlaytestApiResultKind.LocalFileUnavailable:
                    return Verdict(PlaytestUploadFailureKind.Retryable, false);
                // 宣言は走行の開始時に固定される。消えた・長さが変わったファイルはやり直しても戻らない
                // The declaration is fixed when the run starts; a vanished or resized file never comes back on retry
                case PlaytestApiResultKind.LocalFileChanged:
                    return Verdict(FileOrBox(PlaytestUploadFailureKind.PermanentForBox), false);
                case PlaytestApiResultKind.Responded:
                    return failure.Stage == PlaytestUploadStage.SignedPut ? ClassifySignedPut() : Verdict(ClassifyReceiver(), false);
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure), result.Kind, "unknown api result kind");
            }

            #region Internal

            PlaytestUploadFailureDecision Verdict(PlaytestUploadFailureKind kind, bool abortsRun)
            {
                return new PlaytestUploadFailureDecision(kind, abortsRun, description);
            }

            // 必須ファイル（またはパスの無い失敗）なら箱の種別、それ以外はそのファイルだけの見送り
            // The given box-level kind for a required file (or a failure without a path); otherwise only that file is skipped
            PlaytestUploadFailureKind FileOrBox(PlaytestUploadFailureKind boxKind)
            {
                var isRequired = failure.Path.Length == 0 || filePolicy.RankOf(failure.Path) == PlaytestBundleFileRank.Required;
                return isRequired ? boxKind : PlaytestUploadFailureKind.PermanentForFile;
            }

            // トークンが取れなかった理由で分ける。不許可・チケット拒否は待っても直らず、どの箱も同じなので走行を止める
            // Split by why no token was available; not-allowed and a rejected ticket never heal and fail every box, so the run stops
            PlaytestUploadFailureDecision DecideSession()
            {
                switch (result.SessionOutcome)
                {
                    case PlaytestSessionOutcome.NotAllowed:
                    case PlaytestSessionOutcome.TicketRejected:
                        return Verdict(PlaytestUploadFailureKind.SessionRefused, true);
                    case PlaytestSessionOutcome.Unreachable:
                    case PlaytestSessionOutcome.TicketUnavailable:
                        return Verdict(PlaytestUploadFailureKind.Retryable, true);
                    case PlaytestSessionOutcome.MalformedResponse:
                        return Verdict(PlaytestUploadFailureKind.Retryable, false);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(failure), result.SessionOutcome, "a session failure cannot carry this outcome");
                }
            }

            // 受け口（prepare・complete）の応答。取り直しても残る401と403は権利の問題、宣言の衝突・読めない宣言はその箱固有、408/409/429/5xxは一過性
            // A receiver answer (prepare, complete): a surviving 401 or a 403 is a permission matter, a conflicting or unreadable declaration belongs to the box, 408/409/429/5xx are transient
            PlaytestUploadFailureKind ClassifyReceiver()
            {
                var statusCode = result.StatusCode;
                if (statusCode == 401 || statusCode == 403) return PlaytestUploadFailureKind.Unauthorized;
                var reason = statusCode == 409 ? PlaytestReceiverResponseBody.ReadReason(result.Body) : null;
                // 世代と集合の食い違いは何度送っても同じ409になる
                // A generation or set mismatch answers the same 409 however often it is sent
                if (reason == PlaytestReceiverConfig.DeclarationConflictReason) return PlaytestUploadFailureKind.PermanentForBox;
                // 受け口の DECLARED が壊れている。人が受け口側を直すまで通らない
                // The receiver's DECLARED is broken; nothing passes until a person fixes the receiver side
                if (reason == PlaytestReceiverConfig.DeclarationUnreadableReason)
                {
                    Debug.LogError($"[PlaytestReceiver] the receiver cannot read its stored declaration; the box needs a person to repair it on the receiver: {description}");
                    return PlaytestUploadFailureKind.PermanentForBox;
                }
                // それ以外の409はcompleteの「揃っていない」「prepareが無い」。prepareからやり直せば直る
                // Any other 409 is complete's "incomplete" or "not-prepared"; redoing from prepare heals it
                if (statusCode == 408 || statusCode == 409 || statusCode == 429) return PlaytestUploadFailureKind.Retryable;
                if (400 <= statusCode && statusCode < 500) return PlaytestUploadFailureKind.PermanentForBox;
                return PlaytestUploadFailureKind.Retryable;
            }

            // 署名付きURL（R2）へのPUTの応答。バケット全体の失敗は走行ごと止め、403は原因で分け、ファイル固有の4xxは必須なら再試行を経て箱、補助ならそのファイルだけ見送る
            // An answer to the presigned PUT (R2): a bucket-wide failure stops the run, a 403 splits by cause, and a file-specific 4xx retries then counts the box when required, or skips just that file otherwise
            PlaytestUploadFailureDecision ClassifySignedPut()
            {
                var statusCode = result.StatusCode;
                if (PlaytestSignedPutBucketWideFailure.IsBucketWide(result.Body, out var code))
                {
                    Debug.LogWarning($"[PlaytestReceiver] R2 answered {statusCode} {code}, a failure of the whole bucket or setup; no file is skipped and no box is counted for it");
                    return Verdict(PlaytestUploadFailureKind.Retryable, true);
                }
                if (statusCode == 403)
                {
                    var cause = PlaytestSignedPutForbidden.Classify(result.Body);
                    switch (cause)
                    {
                        case PlaytestSignedPutForbiddenCause.ExpiredSignature:
                        case PlaytestSignedPutForbiddenCause.UnrecognizedBody:
                            return Verdict(PlaytestUploadFailureKind.Retryable, false);
                        case PlaytestSignedPutForbiddenCause.Permanent:
                            return Verdict(FileOrBox(PlaytestUploadFailureKind.RetryableThenPermanentForBox), false);
                        default:
                            throw new ArgumentOutOfRangeException(nameof(failure), cause, "unknown forbidden cause");
                    }
                }
                if (statusCode == 408 || statusCode == 429 || statusCode < 400 || 500 <= statusCode) return Verdict(PlaytestUploadFailureKind.Retryable, false);
                return Verdict(FileOrBox(PlaytestUploadFailureKind.RetryableThenPermanentForBox), false);
            }

            #endregion
        }
    }
}
