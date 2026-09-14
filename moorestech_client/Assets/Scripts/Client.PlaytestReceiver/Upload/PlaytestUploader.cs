using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Client.PlaytestReceiver.Http;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload
{
    // 未送の箱を古い順に受け口へ送る。1回の走行で使い捨てる想定で、トークンは走行の中だけで持ち回る
    // Ships pending boxes oldest first; one instance serves one run and carries the token only within it
    public sealed class PlaytestUploader
    {
        private readonly IPlaytestReceiverApi _api;
        private readonly PlaytestSession _session;
        private readonly string _reportOutbox;
        private readonly string _progressOutbox;

        private string _bearerToken;

        public PlaytestUploader(IPlaytestReceiverApi api, PlaytestSession session, string reportOutbox, string progressOutbox)
        {
            _api = api;
            _session = session;
            _reportOutbox = reportOutbox;
            _progressOutbox = progressOutbox;
        }

        public async UniTask<int> UploadPendingAsync(DateTime utcNow, CancellationToken token)
        {
            var boxes = PlaytestOutboxScanner.ScanPending(_reportOutbox, _progressOutbox);
            if (boxes.Count == 0) return 0;

            _bearerToken = await _session.GetValidTokenAsync(utcNow, token);
            if (_bearerToken == null)
            {
                Debug.LogWarning($"[PlaytestReceiver] deferring {boxes.Count} box(es): no valid session token");
                return 0;
            }

            var sent = 0;
            foreach (var box in boxes)
            {
                if (await UploadOneAsync(box, utcNow, token)) sent++;
            }
            return sent;
        }

        private async UniTask<bool> UploadOneAsync(PlaytestOutboxBox box, DateTime utcNow, CancellationToken token)
        {
            var skipped = new List<object>();
            var putCount = 0;

            foreach (var file in PlaytestOutboxScanner.ListPayloadFiles(box.Directory))
            {
                var relativePath = PlaytestOutboxScanner.ToRelativePath(box.Directory, file);
                var skipReason = DescribeSkip(file, relativePath);
                if (skipReason != null)
                {
                    skipped.Add(new { path = relativePath, reason = skipReason });
                    continue;
                }

                var result = await PutWithRefreshAsync(box, relativePath, file, utcNow, token);

                // 取り直しても認可が下りない箱は、期限切れではなく権利の問題。試行を数えて後の走行へ回す
                // A box that stays unauthorized after a refresh is a permission matter, not an expiry; count it and defer
                if (result == null)
                {
                    PlaytestUploadAttemptLog.Increment(box.Directory, $"{relativePath}: the session token could not be renewed");
                    return false;
                }
                if (result.IsSuccess)
                {
                    putCount++;
                    continue;
                }

                // 恒久的な4xxは再試行しても直らない。そのファイルだけ見送り、箱ごと詰まらせない
                // A permanent 4xx never heals on retry, so only that file is dropped instead of stalling the whole box
                if (PlaytestUploadFailurePolicy.IsPermanentForFile(result))
                {
                    skipped.Add(new { path = relativePath, reason = PlaytestUploadFailurePolicy.ToSkipReason(result) });
                    Debug.LogWarning($"[PlaytestReceiver] skipping {PlaytestUploadFailurePolicy.Describe(relativePath, result)}: the receiver refused it permanently");
                    continue;
                }

                PlaytestUploadAttemptLog.Increment(box.Directory, PlaytestUploadFailurePolicy.Describe(relativePath, result));
                return false;
            }

            return await CompleteAsync(box, putCount, skipped, utcNow, token);
        }

        private async UniTask<bool> CompleteAsync(PlaytestOutboxBox box, int putCount, List<object> skipped, DateTime utcNow, CancellationToken token)
        {
            var summary = JsonConvert.SerializeObject(new
            {
                kind = box.Kind,
                id = box.BundleId,
                fileCount = putCount,
                skipped,
                manifest = ReadManifestSummary(box.Directory),
            });

            var completed = await CompleteWithRefreshAsync(box, summary, utcNow, token);
            if (completed == null)
            {
                PlaytestUploadAttemptLog.Increment(box.Directory, "complete: the session token could not be renewed");
                return false;
            }
            if (!completed.IsSuccess)
            {
                PlaytestUploadAttemptLog.Increment(box.Directory, PlaytestUploadFailurePolicy.Describe("complete", completed));
                return false;
            }

            PlaytestUploadAttemptLog.MarkUploaded(box.Directory);
            Debug.Log($"[PlaytestReceiver] uploaded {box.Kind}/{box.BundleId} ({putCount} files, {skipped.Count} skipped)");
            return true;
        }

        // 送れないファイルの理由。送る前に分かるものだけを見て、通信の失敗とは混ぜない
        // Why a file cannot be sent, decided before any request so it never mixes with a transport failure
        private static string DescribeSkip(string absoluteFilePath, string relativePath)
        {
            if (!PlaytestOutboxScanner.IsSendablePath(relativePath))
            {
                Debug.LogWarning($"[PlaytestReceiver] skipping {relativePath}: the receiver only accepts [A-Za-z0-9._-] path segments");
                return "unsupported-characters";
            }

            var length = new FileInfo(absoluteFilePath).Length;
            if (length <= PlaytestReceiverConfig.MaxFileBytes) return null;

            Debug.LogWarning($"[PlaytestReceiver] skipping {relativePath}: {length} bytes exceeds the {PlaytestReceiverConfig.MaxFileBytes} byte limit");
            return "too-large";
        }

        // 401はトークンの期限切れ。取り直して1回だけやり直し、取り直せなければnullで呼び出し側へ返す
        // A 401 means the token expired; it is refreshed and retried exactly once, and a failed refresh returns null
        private async UniTask<PlaytestApiResult> PutWithRefreshAsync(PlaytestOutboxBox box, string relativePath, string absoluteFilePath, DateTime utcNow, CancellationToken token)
        {
            var result = await _api.PutFileAsync(_bearerToken, box.Kind, box.BundleId, relativePath, absoluteFilePath, token);
            if (!PlaytestUploadFailurePolicy.IsUnauthorized(result)) return result;
            if (!await RefreshTokenAsync(utcNow, token)) return null;

            return await _api.PutFileAsync(_bearerToken, box.Kind, box.BundleId, relativePath, absoluteFilePath, token);
        }

        private async UniTask<PlaytestApiResult> CompleteWithRefreshAsync(PlaytestOutboxBox box, string summaryJson, DateTime utcNow, CancellationToken token)
        {
            var result = await _api.PostCompleteAsync(_bearerToken, box.Kind, box.BundleId, summaryJson, token);
            if (!PlaytestUploadFailurePolicy.IsUnauthorized(result)) return result;
            if (!await RefreshTokenAsync(utcNow, token)) return null;

            return await _api.PostCompleteAsync(_bearerToken, box.Kind, box.BundleId, summaryJson, token);
        }

        private async UniTask<bool> RefreshTokenAsync(DateTime utcNow, CancellationToken token)
        {
            Debug.Log("[PlaytestReceiver] the receiver rejected the token; renewing it once and retrying");

            var authenticated = await _session.AuthenticateAsync(utcNow, token);
            if (authenticated.Outcome != PlaytestSessionOutcome.Allowed)
            {
                Debug.LogWarning($"[PlaytestReceiver] could not renew the token: {authenticated.Outcome} {authenticated.Detail}");
                return false;
            }

            _bearerToken = await _session.GetValidTokenAsync(utcNow, token);
            return _bearerToken != null;
        }

        // manifest.json はplan Bが書く。取り込み側の一覧表示用に本文をそのまま要約へ載せる
        // plan B writes manifest.json; its raw text rides along in the summary for the ingest side's listing
        private static string ReadManifestSummary(string boxDirectory)
        {
            var path = Path.Combine(boxDirectory, "manifest.json");
            if (!File.Exists(path)) return null;
            return File.ReadAllText(path);
        }
    }
}
