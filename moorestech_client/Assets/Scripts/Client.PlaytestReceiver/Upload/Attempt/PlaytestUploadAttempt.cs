using System.Collections.Generic;
using System.IO;
using System.Threading;
using Client.PlaytestReceiver.Http;
using Cysharp.Threading.Tasks;
using Game.Paths;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload.Attempt
{
    // 箱1つの1試行（prepare→署名付きURLへPUT→complete）。再試行の判断は呼び出し側（PlaytestUploader）が持つ
    // One attempt for one box (prepare→PUT to the presigned URLs→complete); the retry decision stays with the caller (PlaytestUploader)
    internal sealed class PlaytestUploadAttempt
    {
        private readonly IPlaytestReceiverApi _api;
        private readonly PlaytestSession _session;

        public PlaytestUploadAttempt(IPlaytestReceiverApi api, PlaytestSession session)
        {
            _api = api;
            _session = session;
        }

        // 1試行。成功はnull、失敗は「どこで」と結果を返す。既に送れたファイルはprepareに再宣言しつつPUTだけ飛ばす（completeの照合は全宣言を見る）
        // One attempt: null on success, else where it failed and the result. Files already sent are re-declared to prepare but their PUT is skipped (complete verifies the whole declaration)
        public async UniTask<PlaytestUploadAttemptFailure> RunAsync(PlaytestOutboxBox box, PlaytestBoxDeclaration declaration, HashSet<string> sentPaths, CancellationToken token)
        {
            var prepared = await _session.SendAuthorizedAsync(new PlaytestPrepareCall(_api, box, declaration.Files), token);
            if (!prepared.IsSuccess) return new PlaytestUploadAttemptFailure("prepare", prepared, false);
            if (!PlaytestPrepareResponse.TryParse(prepared.Body, out var response, out var detail))
            {
                return new PlaytestUploadAttemptFailure("prepare", PlaytestApiResult.TransportFailure(detail), false);
            }
            if (response.Outcome == PlaytestPrepareOutcome.Prepared)
            {
                foreach (var upload in response.Uploads)
                {
                    if (sentPaths.Contains(upload.Path)) continue;
                    var file = FindDeclared(declaration, upload.Path);
                    if (file == null)
                    {
                        Debug.LogWarning($"[PlaytestReceiver] the receiver returned an upload for an undeclared path: {upload.Path}");
                        return new PlaytestUploadAttemptFailure("prepare", PlaytestApiResult.TransportFailure($"undeclared path in prepare response: {upload.Path}"), false);
                    }
                    var put = await _api.PutToSignedUrlAsync(upload.Url, file.AbsolutePath, file.Bytes, token);
                    if (!put.IsSuccess) return new PlaytestUploadAttemptFailure(upload.Path, put, true);
                    sentPaths.Add(upload.Path);
                }
            }
            var completed = await _session.SendAuthorizedAsync(new PlaytestCompleteCall(_api, box, ComposeSupplement(box, declaration)), token);
            if (completed.IsSuccess) return null;
            // 409 は受け口が数えた欠損。欠けたパスを送信済みから外さないと次の試行で PUT が全部飛び同じ 409 を繰り返す（不明なら全部送り直す）
            // A 409 carries the receiver's count of what is missing; unless those paths leave the sent set, the next attempt skips every PUT and repeats the same 409 (when unknown, resend all)
            if (completed.Kind == PlaytestApiResultKind.Responded && completed.StatusCode == 409) ForgetMissing(completed.Body, sentPaths);
            return new PlaytestUploadAttemptFailure("complete", completed, false);
        }

        private static void ForgetMissing(string body, HashSet<string> sentPaths)
        {
            // 受け口の応答は外部入力のJSON。読めなければ全部送り直す側に倒す
            // The receiver's body is external JSON; when unreadable, fall back to resending everything
            try
            {
                if (JObject.Parse(body)["missing"] is JArray missing)
                {
                    foreach (var entry in missing) sentPaths.Remove(entry.Value<string>("path") ?? "");
                    return;
                }
                Debug.LogWarning("[PlaytestReceiver] complete answered 409 without a missing list; resending every file");
                sentPaths.Clear();
            }
            catch (JsonException exception)
            {
                Debug.LogWarning($"[PlaytestReceiver] complete answered 409 with an unreadable body; resending every file: {exception.Message}");
                sentPaths.Clear();
            }
        }

        private static PlaytestDeclaredFile FindDeclared(PlaytestBoxDeclaration declaration, string path)
        {
            foreach (var file in declaration.Files)
            {
                if (file.Path == path) return file;
            }
            return null;
        }

        // completeの補足。manifest原文と見送り一覧だけを載せ、ファイル一覧は受け口が照合して決める
        // The complete supplement: only the raw manifest and the skips; the file list is settled by the receiver's verification
        private static string ComposeSupplement(PlaytestOutboxBox box, PlaytestBoxDeclaration declaration)
        {
            var manifestPath = Path.Combine(box.Directory, BugReportBundleLayout.ManifestFileName);
            return JsonConvert.SerializeObject(new
            {
                skipped = declaration.Skipped,
                manifest = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : null,
            });
        }
    }

    // 失敗した試行の「どこで」と結果。署名付きURLへのPUTかどうかで403の読み方が変わる
    // Where a failed attempt failed and its result; whether it was the presigned PUT changes how a 403 is read
    internal sealed class PlaytestUploadAttemptFailure
    {
        public readonly string What;
        public readonly PlaytestApiResult Result;
        public readonly bool IsSignedPut;

        public PlaytestUploadAttemptFailure(string what, PlaytestApiResult result, bool isSignedPut)
        {
            What = what;
            Result = result;
            IsSignedPut = isSignedPut;
        }
    }
}
