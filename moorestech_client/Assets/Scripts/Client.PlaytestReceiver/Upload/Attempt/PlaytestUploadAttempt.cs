using System.Collections.Generic;
using System.IO;
using System.Threading;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Http.Responses;
using Cysharp.Threading.Tasks;
using Game.Paths;
using Newtonsoft.Json;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload.Attempt
{
    // 箱1つの1試行（prepare→署名付きURLへPUT→complete）。再試行の判断は呼び出し側（PlaytestUploader）が持つ
    // One attempt for one box (prepare→PUT to the presigned URLs→complete); the retry decision stays with the caller (PlaytestUploader)
    internal sealed class PlaytestUploadAttempt
    {
        private readonly IPlaytestReceiverApi _api;
        private readonly PlaytestSession _session;

        // PUTが412（既にある）を返したキー。試行を跨いで持つ（このインスタンスは箱ごとに作られる）
        // Keys whose PUT answered 412 (already there); kept across attempts (one instance is made per box)
        private readonly HashSet<string> _alreadyStoredPaths = new();

        public PlaytestUploadAttempt(IPlaytestReceiverApi api, PlaytestSession session)
        {
            _api = api;
            _session = session;
        }

        // 1試行。成功はnull、失敗は段と結果を返す。PUTするのはprepareがURLを返したファイルだけ（R2に揃っている分は受け口が外す）
        // One attempt: null on success, else the stage and result. Only files prepare returned a URL for are PUT (the receiver leaves out what R2 already holds)
        public async UniTask<PlaytestUploadAttemptFailure> RunAsync(PlaytestOutboxBox box, PlaytestBoxDeclaration declaration, HashSet<string> sentPaths, CancellationToken token)
        {
            // 受け口は宣言の拡大を拒む。送る前に宣言を箱へ残し、次の走行の宣言をその範囲に留める
            // The receiver refuses a grown declaration; the declaration is kept in the box before sending so the next run stays within it
            PlaytestUploadDeclaredRecord.Write(box.Directory, declaration.Files);
            var prepared = await _session.SendAuthorizedAsync(new PlaytestPrepareCall(_api, box, declaration.Files), token);
            if (!prepared.IsSuccess) return PlaytestUploadAttemptFailure.AtPrepare(prepared);
            if (!PlaytestPrepareResponse.TryParse(prepared.Body, out var response, out var detail))
            {
                return PlaytestUploadAttemptFailure.AtPrepare(PlaytestApiResult.MalformedResponse(prepared.StatusCode, detail));
            }
            if (response.Outcome == PlaytestPrepareOutcome.Prepared)
            {
                foreach (var upload in response.Uploads)
                {
                    if (sentPaths.Contains(upload.Path)) continue;
                    var file = FindDeclared(upload.Path);
                    if (file == null)
                    {
                        Debug.LogWarning($"[PlaytestReceiver] the receiver returned an upload for an undeclared path: {upload.Path}");
                        return PlaytestUploadAttemptFailure.AtPrepare(PlaytestApiResult.MalformedResponse(prepared.StatusCode, $"undeclared path in prepare response: {upload.Path}"));
                    }
                    var put = await _api.PutToSignedUrlAsync(upload.Url, file.AbsolutePath, file.Bytes, token);
                    if (IsAlreadyStored(put))
                    {
                        // If-None-Match: * の412は同じキーが既にR2にある印。上書きせず送信済みとして進み、長さと存在はcompleteの照合に任せる
                        // A 412 under If-None-Match: * means the key already exists in R2; move on as sent without overwriting and leave length and presence to complete
                        Debug.Log($"[PlaytestReceiver] {upload.Path} is already stored (HTTP 412); treating it as sent and leaving it to complete's verification");
                        sentPaths.Add(upload.Path);
                        _alreadyStoredPaths.Add(upload.Path);
                        continue;
                    }
                    if (!put.IsSuccess) return PlaytestUploadAttemptFailure.AtSignedPut(upload.Path, put);
                    sentPaths.Add(upload.Path);
                }
            }
            var completed = await _session.SendAuthorizedAsync(new PlaytestCompleteCall(_api, box, ComposeSupplement()), token);
            if (completed.IsSuccess)
            {
                if (PlaytestReceiverResponseBody.IsReady(completed.Body)) return null;
                return PlaytestUploadAttemptFailure.AtComplete(PlaytestApiResult.MalformedResponse(completed.StatusCode, $"complete answered without ready:true: {completed.Body}"));
            }
            if (completed.Kind != PlaytestApiResultKind.Responded || completed.StatusCode != 409) return PlaytestUploadAttemptFailure.AtComplete(completed);
            // 412で送信済みとしたキーが欠けに出たら、やり直しても同じ409を数えずに繰り返すだけなので食い違いとして返す
            // When a key taken as sent on a 412 shows up missing, retrying would only repeat the same uncounted 409, so it is returned as a mismatch
            foreach (var missingPath in PlaytestCompleteMissingPaths.Forget(completed.Body, sentPaths))
            {
                if (_alreadyStoredPaths.Contains(missingPath)) return PlaytestUploadAttemptFailure.StoredObjectMismatchAt(missingPath, completed);
            }
            return PlaytestUploadAttemptFailure.AtComplete(completed);

            #region Internal

            bool IsAlreadyStored(PlaytestApiResult put)
            {
                return put.Kind == PlaytestApiResultKind.Responded && put.StatusCode == 412;
            }

            PlaytestDeclaredFile FindDeclared(string path)
            {
                foreach (var file in declaration.Files)
                {
                    if (file.Path == path) return file;
                }
                return null;
            }

            // completeの補足。manifest原文と見送り一覧だけを載せ、ファイル一覧は受け口が照合して決める
            // The complete supplement: only the raw manifest and the skips; the file list is settled by the receiver's verification
            string ComposeSupplement()
            {
                var manifestPath = Path.Combine(box.Directory, BugReportBundleLayout.ManifestFileName);
                return JsonConvert.SerializeObject(new
                {
                    skipped = declaration.Skipped,
                    manifest = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : null,
                });
            }

            #endregion
        }
    }
}
