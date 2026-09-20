using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload.Attempt;
using Client.PlaytestReceiver.Upload.Failure;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload
{
    // 未送の箱を古い順に受け口へ送る。トークンの寿命と401時の取り直しはセッションに任せる
    // Ships pending boxes oldest first; the token lifetime and the 401 refresh are left to the session
    public sealed class PlaytestUploader
    {
        private enum BoxOutcome
        {
            Sent,
            BoxDeferred,
            RunAborted,
        }

        private readonly IPlaytestReceiverApi _api;
        private readonly PlaytestSession _session;
        private readonly PlaytestOutboxDirectories _directories;
        private readonly PlaytestUploadRetrySchedule _retry;

        public PlaytestUploader(IPlaytestReceiverApi api, PlaytestSession session, PlaytestOutboxDirectories directories, PlaytestUploadRetrySchedule retry)
        {
            _api = api;
            _session = session;
            _directories = directories;
            _retry = retry;
        }

        public async UniTask<int> UploadPendingAsync(CancellationToken token)
        {
            var boxes = PlaytestOutboxScanner.ScanPending(_directories);
            var sent = 0;
            for (var index = 0; index < boxes.Count; index++)
            {
                var outcome = await UploadWithinBookkeepingBoundaryAsync(boxes[index], token);
                if (outcome == BoxOutcome.Sent) sent++;
                if (outcome != BoxOutcome.RunAborted) continue;

                Debug.LogWarning($"[PlaytestReceiver] stopping this run; {boxes.Count - index} box(es) carried over to a later run");
                break;
            }
            return sent;
        }

        // 試行中のファイルI/Oは試行失敗としてDecideへ回る。ここで捕まるのは印（UPLOAD_ATTEMPTS等）の書き込み自体の失敗だけで、箱へ記録できないので理由をログへ残して持ち越す
        // File I/O during an attempt already goes to Decide as a failed attempt; only a failure to write the markers themselves (UPLOAD_ATTEMPTS and the like) lands here, and since the box cannot record it, the reason is logged and the box deferred
        private async UniTask<BoxOutcome> UploadWithinBookkeepingBoundaryAsync(PlaytestOutboxBox box, CancellationToken token)
        {
            try
            {
                return await UploadOneAsync(box, token);
            }
            catch (IOException exception)
            {
                Debug.LogError($"[PlaytestReceiver] could not record the upload state of {box.BundleId}; deferring it: {exception.GetType().Name}: {exception.Message}");
                return BoxOutcome.BoxDeferred;
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogError($"[PlaytestReceiver] could not record the upload state of {box.BundleId}; deferring it: {exception.Message}");
                return BoxOutcome.BoxDeferred;
            }
        }

        // prepare→署名付きURLへPUT→completeを1試行とし、一過性の失敗は表の回数だけ待って同じ箱をやり直す。送れたファイルは次の試行で飛ばす
        // One attempt is prepare→PUT to the presigned URLs→complete; a transient failure waits per the schedule and retries the same box, skipping files already sent
        private async UniTask<BoxOutcome> UploadOneAsync(PlaytestOutboxBox box, CancellationToken token)
        {
            var attemptRunner = new PlaytestUploadAttempt(_api, _session);
            var sentPaths = new HashSet<string>();
            PlaytestBoxDeclaration declaration = null;
            var attempt = 0;
            while (true)
            {
                var step = await RunAttemptAsync();
                if (step.Outcome.HasValue) return step.Outcome.Value;
                // 数えるのは再試行で直らない失敗だけ。一過性の失敗は表が尽きたら数えずに持ち越し、到達不能やトークン不調なら走行ごと止める
                // Only failures retrying cannot heal are counted; transient ones defer uncounted once the schedule runs out, and unreachability stops the run
                var decision = PlaytestUploadFailurePolicy.Decide(step.Failure, box.FilePolicy);
                switch (decision.Kind)
                {
                    case PlaytestUploadFailureKind.PermanentForFile:
                        declaration = SkipRefusedFile(step.Failure, decision);
                        if (IsUnsendable(declaration)) return BoxOutcome.BoxDeferred;
                        continue;
                    case PlaytestUploadFailureKind.Retryable:
                    case PlaytestUploadFailureKind.RetryableThenPermanentForBox:
                        break;
                    case PlaytestUploadFailureKind.SessionRefused:
                        Debug.LogWarning($"[PlaytestReceiver] the session was refused; not counting {box.BundleId} and stopping the run: {decision.Description}");
                        return BoxOutcome.RunAborted;
                    default:
                        PlaytestUploadAttemptLog.Increment(box.Directory, decision.Description);
                        return BoxOutcome.BoxDeferred;
                }
                if (_retry.Delays.Count <= attempt) return GiveUpRetrying(decision);
                var delay = _retry.Delays[attempt];
                attempt++;
                Debug.LogWarning($"[PlaytestReceiver] {decision.Description}; retry {attempt}/{_retry.Delays.Count} in {delay.TotalSeconds:0}s");
                // 待ち0はフレームを跨がず即座にやり直す（待ちゼロの表でテストを同期で完結させる）
                // A zero wait retries at once without crossing a frame, so zero-wait tests complete synchronously
                if (TimeSpan.Zero < delay) await UniTask.Delay(delay, DelayType.Realtime, PlayerLoopTiming.Update, token);
            }

            #region Internal

            // 1試行。箱のファイルの読み書きは外部境界（消えた・他プロセスに掴まれた）で、例外は試行失敗に変えて判定をDecide一本に集める
            // One attempt; the box's file I/O is an external boundary (vanished or locked files), so an exception becomes a failed attempt and Decide stays the single verdict
            async UniTask<(BoxOutcome? Outcome, PlaytestUploadAttemptFailure Failure)> RunAttemptAsync()
            {
                try
                {
                    if (declaration == null)
                    {
                        declaration = PlaytestBoxDeclaration.Build(box);
                        if (IsUnsendable(declaration)) return (BoxOutcome.BoxDeferred, null);
                    }
                    var failure = await attemptRunner.RunAsync(box, declaration, sentPaths, token);
                    if (failure != null) return (null, failure);
                    PlaytestUploadAttemptLog.MarkUploaded(box.Directory);
                    Debug.Log($"[PlaytestReceiver] uploaded {PlaytestUploadPath.KindSegment(box.Kind)}/{box.BundleId} ({declaration.Files.Count} files, {declaration.Skipped.Count} skipped)");
                    return (BoxOutcome.Sent, null);
                }
                catch (IOException exception)
                {
                    return (null, PlaytestUploadAttemptFailure.AtLocalFiles(PlaytestApiResult.LocalFileUnavailable($"{exception.GetType().Name}: {exception.Message}")));
                }
                catch (UnauthorizedAccessException exception)
                {
                    return (null, PlaytestUploadAttemptFailure.AtLocalFiles(PlaytestApiResult.LocalFileUnavailable($"access denied: {exception.Message}")));
                }
            }

            // 表を使い切った。必須ファイルの拒否ならここで箱を数え、一過性なら数えずに持ち越す（到達不能等は走行ごと止める）
            // The schedule is spent; a required file's refusal now counts the box, a transient failure defers uncounted (unreachability and the like stop the run)
            BoxOutcome GiveUpRetrying(PlaytestUploadFailureDecision decision)
            {
                var description = $"{decision.Description} (after {attempt} retries)";
                if (decision.Kind == PlaytestUploadFailureKind.RetryableThenPermanentForBox)
                {
                    PlaytestUploadAttemptLog.Increment(box.Directory, description);
                    return BoxOutcome.BoxDeferred;
                }
                PlaytestUploadAttemptLog.LogRetryable(box.Directory, description);
                return decision.AbortsRun ? BoxOutcome.RunAborted : BoxOutcome.BoxDeferred;
            }

            // 箱の格付けで必須のファイルが欠けた箱・送るものが無い箱は受け口へ行かずに恒久失敗として数える（偽のHTTP応答を合成しない）
            // A box missing a file its policy ranks required, or with nothing to send, never reaches the receiver and is counted as permanent (no forged HTTP response)
            bool IsUnsendable(PlaytestBoxDeclaration candidate)
            {
                if (candidate.MissingRequiredReason != null)
                {
                    PlaytestUploadAttemptLog.Increment(box.Directory, $"not sending a box without its required files: {candidate.MissingRequiredReason}");
                    return true;
                }
                if (candidate.Files.Count != 0) return false;
                PlaytestUploadAttemptLog.Increment(box.Directory, $"nothing to send: every file was skipped ({candidate.Skipped.Count} skipped)");
                return true;
            }

            // 1ファイルだけが直らない。見送りを箱へ記録して次回起動以降も宣言へ戻さず（受け口は宣言の拡大を拒む）、縮小した宣言を次の世代でやり直す
            // A single file never heals; the skip is recorded in the box so later runs never re-declare it (the receiver refuses a grown declaration), and the box retries with the shrunk one as the next generation
            PlaytestBoxDeclaration SkipRefusedFile(PlaytestUploadAttemptFailure refused, PlaytestUploadFailureDecision decision)
            {
                var reason = PlaytestUploadFailureDescription.SkipReasonOf(refused);
                Debug.LogWarning($"[PlaytestReceiver] skipping {refused.Path} of {box.BundleId} and retrying from prepare: {decision.Description}");
                PlaytestUploadSkipRecord.Append(box.Directory, refused.Path, reason);
                return declaration.WithSkipped(refused.Path, reason);
            }

            #endregion
        }
    }
}
