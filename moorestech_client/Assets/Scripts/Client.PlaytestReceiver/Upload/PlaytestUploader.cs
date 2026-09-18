using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload.Attempt;
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
            var boxes = PlaytestOutboxScanner.ScanPending(_directories.ReportOutbox, _directories.ProgressOutbox);
            var sent = 0;
            for (var index = 0; index < boxes.Count; index++)
            {
                var outcome = await UploadWithinBoxBoundaryAsync(boxes[index], token);
                if (outcome == BoxOutcome.Sent) sent++;
                if (outcome != BoxOutcome.RunAborted) continue;

                Debug.LogWarning($"[PlaytestReceiver] stopping this run; {boxes.Count - index} box(es) carried over to a later run");
                break;
            }
            return sent;
        }

        // 箱の中身のファイルI/Oは外部境界（消えた・他プロセスに掴まれた）。1箱の例外で走行全体を恒久に止めず、数えて次の箱へ進む
        // File I/O inside a box is an external boundary (vanished or locked files); one box's exception is counted instead of halting every run
        private async UniTask<BoxOutcome> UploadWithinBoxBoundaryAsync(PlaytestOutboxBox box, CancellationToken token)
        {
            try
            {
                return await UploadOneAsync(box, token);
            }
            catch (IOException exception)
            {
                PlaytestUploadAttemptLog.Increment(box.Directory, $"file I/O failed: {exception.GetType().Name}: {exception.Message}");
                return BoxOutcome.BoxDeferred;
            }
            catch (UnauthorizedAccessException exception)
            {
                PlaytestUploadAttemptLog.Increment(box.Directory, $"file access denied: {exception.Message}");
                return BoxOutcome.BoxDeferred;
            }
        }

        // prepare→署名付きURLへPUT→completeを1試行とし、一過性の失敗は表の回数だけ待って同じ箱をやり直す。送れたファイルは次の試行で飛ばす
        // One attempt is prepare→PUT to the presigned URLs→complete; a transient failure waits per the schedule and retries the same box, skipping files already sent
        private async UniTask<BoxOutcome> UploadOneAsync(PlaytestOutboxBox box, CancellationToken token)
        {
            var declaration = PlaytestBoxDeclaration.Build(box);
            if (declaration.Files.Count == 0)
            {
                // 送るものが1つも無い箱は受け口へ行かずに恒久失敗として数える（偽のHTTP応答を合成しない）
                // A box with nothing sendable never reaches the receiver; it is counted as permanent without forging an HTTP response
                PlaytestUploadAttemptLog.Increment(box.Directory, $"nothing to send: every file was skipped ({declaration.Skipped.Count} skipped)");
                return BoxOutcome.BoxDeferred;
            }
            var attemptRunner = new PlaytestUploadAttempt(_api, _session);
            var sentPaths = new HashSet<string>();
            var attempt = 0;
            while (true)
            {
                var failure = await attemptRunner.RunAsync(box, declaration, sentPaths, token);
                if (failure == null)
                {
                    PlaytestUploadAttemptLog.MarkUploaded(box.Directory);
                    Debug.Log($"[PlaytestReceiver] uploaded {PlaytestUploadPath.KindSegment(box.Kind)}/{box.BundleId} ({declaration.Files.Count} files, {declaration.Skipped.Count} skipped)");
                    return BoxOutcome.Sent;
                }
                // 数えるのは再試行で直らない失敗だけ。一過性の失敗は表が尽きたら数えずに持ち越し、到達不能やトークン不調なら走行ごと止める
                // Only failures retrying cannot heal are counted; transient ones defer uncounted once the schedule runs out, and unreachability stops the run
                var description = PlaytestUploadFailurePolicy.Describe(failure.What, failure.Result);
                if (PlaytestUploadFailurePolicy.Classify(failure.Result, failure.IsSignedPut) != PlaytestUploadFailureKind.Retryable)
                {
                    PlaytestUploadAttemptLog.Increment(box.Directory, description);
                    return BoxOutcome.BoxDeferred;
                }
                if (_retry.Delays.Count <= attempt)
                {
                    PlaytestUploadAttemptLog.LogRetryable(box.Directory, $"{description} (after {attempt} retries)");
                    return PlaytestUploadFailurePolicy.AbortsRun(failure.Result) ? BoxOutcome.RunAborted : BoxOutcome.BoxDeferred;
                }
                var delay = _retry.Delays[attempt];
                attempt++;
                Debug.LogWarning($"[PlaytestReceiver] {description}; retry {attempt}/{_retry.Delays.Count} in {delay.TotalSeconds:0}s");
                // 待ち0はフレームを跨がず即座にやり直す（待ちゼロの表でテストを同期で完結させる）
                // A zero wait retries at once without crossing a frame, so zero-wait tests complete synchronously
                if (TimeSpan.Zero < delay) await UniTask.Delay(delay, DelayType.Realtime, PlayerLoopTiming.Update, token);
            }
        }
    }
}
