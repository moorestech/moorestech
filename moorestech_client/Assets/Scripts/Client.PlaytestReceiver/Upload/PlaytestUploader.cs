using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Client.PlaytestReceiver.Http;
using Cysharp.Threading.Tasks;
using Game.Paths;
using Newtonsoft.Json;
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

        public PlaytestUploader(IPlaytestReceiverApi api, PlaytestSession session, PlaytestOutboxDirectories directories)
        {
            _api = api;
            _session = session;
            _directories = directories;
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

        private async UniTask<BoxOutcome> UploadOneAsync(PlaytestOutboxBox box, CancellationToken token)
        {
            var skipped = new List<object>();
            var putCount = 0;

            foreach (var file in PlaytestOutboxScanner.ListPayloadFiles(box.Directory))
            {
                var relativePath = PlaytestOutboxScanner.ToRelativePath(box.Directory, file);
                var skip = DescribeSkip(file, relativePath);
                if (skip != null)
                {
                    skipped.Add(skip);
                    continue;
                }

                var result = await _session.SendAuthorizedAsync(new PlaytestPutFileCall(_api, box, relativePath, file), token);
                if (result.IsSuccess)
                {
                    putCount++;
                    continue;
                }

                // 何度送っても直らないファイルだけ見送り、箱ごと詰まらせない
                // Only a file that can never succeed is dropped, so the box as a whole does not stall
                if (PlaytestUploadFailurePolicy.Classify(result) == PlaytestUploadFailureKind.PermanentForFile)
                {
                    skipped.Add(new { path = relativePath, reason = PlaytestUploadFailurePolicy.ToSkipReason(result) });
                    Debug.LogWarning($"[PlaytestReceiver] skipping {PlaytestUploadFailurePolicy.Describe(relativePath, result)}: it can never be accepted");
                    continue;
                }

                return Defer(relativePath, result);
            }

            var completed = await _session.SendAuthorizedAsync(new PlaytestCompleteCall(_api, box, ComposeSummary()), token);
            if (!completed.IsSuccess) return Defer("complete", completed);

            PlaytestUploadAttemptLog.MarkUploaded(box.Directory);
            Debug.Log($"[PlaytestReceiver] uploaded {PlaytestUploadPath.KindSegment(box.Kind)}/{box.BundleId} ({putCount} files, {skipped.Count} skipped)");
            return BoxOutcome.Sent;

            #region Internal

            // 送る前に分かる見送り理由だけを返す。パスの安全性はクライアントの送信口が1箇所で検査する
            // Returns only the skip reasons knowable before any request; path safety is checked once at the client's send site
            object DescribeSkip(string absoluteFilePath, string relativePath)
            {
                // 先頭セグメントが受け口の予約名と衝突すると405やREADY/ACKEDの上書きになる。送信前に見送る
                // A first segment colliding with a receiver-reserved name would 405 or overwrite READY/ACKED; skip before sending
                var firstSegment = relativePath.Split('/')[0];
                if (0 <= Array.IndexOf(PlaytestOutboxScanner.ReservedUploadSegments, firstSegment))
                {
                    Debug.LogWarning($"[PlaytestReceiver] skipping {relativePath}: its first segment is a reserved name on the receiver");
                    return new { path = relativePath, reason = "reserved-name" };
                }

                var length = new FileInfo(absoluteFilePath).Length;
                if (length <= PlaytestReceiverConfig.MaxFileBytes) return null;

                Debug.LogWarning($"[PlaytestReceiver] skipping {relativePath}: {length} bytes exceeds the {PlaytestReceiverConfig.MaxFileBytes} byte limit");
                return new { path = relativePath, reason = "too-large", bytes = length };
            }

            // 数えるのは再試行で直らない失敗だけ。一時的な失敗は数えずに持ち越し、到達不能やトークン不調なら走行ごと止める
            // Only failures retrying cannot heal are counted; transient ones defer uncounted, and unreachability stops the run
            BoxOutcome Defer(string what, PlaytestApiResult result)
            {
                var description = PlaytestUploadFailurePolicy.Describe(what, result);
                if (PlaytestUploadFailurePolicy.Classify(result) != PlaytestUploadFailureKind.Retryable)
                {
                    PlaytestUploadAttemptLog.Increment(box.Directory, description);
                    return BoxOutcome.BoxDeferred;
                }

                PlaytestUploadAttemptLog.LogRetryable(box.Directory, description);
                return PlaytestUploadFailurePolicy.AbortsRun(result) ? BoxOutcome.RunAborted : BoxOutcome.BoxDeferred;
            }

            // manifest.jsonはplanBが書く。取り込み側の一覧用に生テキストを要約へ転記する
            // plan B writes manifest.json; its raw text rides along in the summary for the ingest side's listing
            string ComposeSummary()
            {
                var manifestPath = Path.Combine(box.Directory, BugReportBundleLayout.ManifestFileName);
                return JsonConvert.SerializeObject(new
                {
                    kind = PlaytestUploadPath.KindSegment(box.Kind),
                    id = box.BundleId,
                    fileCount = putCount,
                    skipped,
                    manifest = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : null,
                });
            }

            #endregion
        }
    }
}
