using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.PlaytestReceiver.Http
{
    // 受け口への実HTTP。HttpClientは接続を使い回すため1本を持ち回る（前例 Client.WebUiHost/Vite/ViteHealthProbe.cs）
    // Real HTTP to the receiver; a single HttpClient is reused for connection pooling (precedent: ViteHealthProbe)
    public sealed class PlaytestReceiverClient : IPlaytestReceiverApi
    {
        private static readonly HttpClient Client = CreateClient();

        private readonly string _baseUrl;

        public PlaytestReceiverClient(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
        }

        public UniTask<PlaytestApiResult> PostSessionAsync(string ticketHex, CancellationToken token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/session")
            {
                Content = new StringContent($"{{\"ticket\":\"{ticketHex}\"}}", Encoding.UTF8, "application/json"),
            };
            return SendWithHttpTimeoutAsync(request, token);
        }

        public UniTask<PlaytestApiResult> PostPrepareAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, int generation, IReadOnlyList<PlaytestDeclaredFile> files, CancellationToken token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/uploads/{PlaytestUploadPath.ForPrepare(kind, bundleId)}")
            {
                Content = new StringContent(ComposeDeclarationBody(generation, files), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            return SendWithHttpTimeoutAsync(request, token);
        }

        // 宣言のワイヤ表現。世代と path・bytes だけを載せ、AbsolutePath は決して出さない（テストで固定）
        // The wire form of the declaration: the generation plus path and bytes only, never AbsolutePath (pinned by a test)
        internal static string ComposeDeclarationBody(int generation, IReadOnlyList<PlaytestDeclaredFile> files)
        {
            var declared = new JArray();
            foreach (var file in files) declared.Add(new JObject { ["path"] = file.Path, ["bytes"] = file.Bytes });
            return new JObject { ["generation"] = generation, ["files"] = declared }.ToString(Newtonsoft.Json.Formatting.None);
        }

        // 署名付きURLへの直接PUT。Bearerは付けず（署名が権限）、Content-Lengthは署名に含まれるため必ず明示する
        // The direct PUT to the presigned URL: no bearer (the signature is the authority) and Content-Length is explicit because it is signed
        public async UniTask<PlaytestApiResult> PutToSignedUrlAsync(string signedUrl, string absoluteFilePath, long bytes, CancellationToken token)
        {
            if (!File.Exists(absoluteFilePath))
            {
                Debug.LogWarning($"[PlaytestReceiver] {absoluteFilePath} disappeared before upload");
                return PlaytestApiResult.LocalFileChanged($"{absoluteFilePath} does not exist");
            }

            // 手元のディスクI/O境界。開けない失敗（ロック・権限）は到達失敗と混ぜず、一過性の手元の問題として分ける
            // Local disk I/O boundary; failing to open (a lock, permissions) is kept apart from unreachability as a transient local problem
            Stream fileStream;
            try
            {
                fileStream = File.OpenRead(absoluteFilePath);
            }
            catch (IOException exception)
            {
                return PlaytestApiResult.LocalFileUnavailable($"{absoluteFilePath}: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                return PlaytestApiResult.LocalFileUnavailable($"{absoluteFilePath}: {exception.Message}");
            }

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            var idleTimeoutSeconds = PlaytestReceiverConfig.UploadIdleTimeoutSeconds;
            var content = new IdleTimeoutFileContent(fileStream, bytes, deadline, TimeSpan.FromSeconds(idleTimeoutSeconds), TimeSpan.FromSeconds(PlaytestReceiverConfig.HttpTimeoutSeconds));
            var result = await SendAsync(CreateSignedPutRequest(signedUrl, content), deadline, $"no upload progress for {idleTimeoutSeconds}s or no answer after the body", token);
            // 送信中に手元のファイルが読めなくなった失敗は、到達失敗ではなく手元の問題として返す（長さの変化は恒久、I/O障害は一過性）
            // A local read failure mid-send is returned as a local problem, not unreachability (a length change is permanent, an I/O fault transient)
            if (content.LocalReadFailure == null) return result;
            var detail = $"{absoluteFilePath}: {content.LocalReadFailure}";
            return content.LocalFileChanged ? PlaytestApiResult.LocalFileChanged(detail) : PlaytestApiResult.LocalFileUnavailable(detail);
        }

        // 署名付きPUTの要求。If-None-Match: * は署名に含まれており（受け口の契約）、既にあるキーを上書きさせない。Bearerは付けない
        // The presigned PUT request; If-None-Match: * is part of the signature (the receiver's contract) and forbids overwriting an existing key; no bearer is attached
        internal static HttpRequestMessage CreateSignedPutRequest(string signedUrl, HttpContent content)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, signedUrl) { Content = content };
            request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Any);
            return request;
        }

        public UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, string supplementJson, CancellationToken token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/uploads/{PlaytestUploadPath.ForComplete(kind, bundleId)}")
            {
                Content = new StringContent(supplementJson, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            return SendWithHttpTimeoutAsync(request, token);
        }

        // 期限は呼び出しごとに与える。HttpClient.Timeoutは本文送信を含む全体に効き、大きい箱を回線速度で殺すため使わない
        // The deadline is per call; HttpClient.Timeout covers body upload too and would kill big bundles on slow lines
        private static async UniTask<PlaytestApiResult> SendWithHttpTimeoutAsync(HttpRequestMessage request, CancellationToken token)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            var timeoutSeconds = PlaytestReceiverConfig.HttpTimeoutSeconds;
            deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            return await SendAsync(request, deadline, $"timed out after {timeoutSeconds}s", token);
        }

        // 到達失敗・タイムアウト・アイドル切れをTransportFailureへ隔離する唯一の境界。呼び出し側は種別と状態コードだけを見る
        // The single boundary isolating unreachability, timeouts and an idle cut into TransportFailure; callers only see the kind and status code
        private static async UniTask<PlaytestApiResult> SendAsync(HttpRequestMessage request, CancellationTokenSource deadline, string cutDescription, CancellationToken token)
        {
            // 宛先の整形も境界の内側で行う。相対URL等で例外になっても走行を落とさず到達失敗へ畳む
            // Even formatting the target happens inside the boundary, so a relative URL folds into a transport failure instead of ending the run
            var target = "an unformattable target";
            try
            {
                using (request)
                {
                    target = DescribeTargetForLog(request.RequestUri);
                    using var response = await Client.SendAsync(request, deadline.Token);
                    var body = await response.Content.ReadAsStringAsync();
                    return PlaytestApiResult.Responded((int)response.StatusCode, body);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"[PlaytestReceiver] request to {target} {cutDescription}");
                return PlaytestApiResult.TransportFailure(cutDescription);
            }
            catch (Exception exception)
            {
                // 例外の型名も残す。到達失敗に見える実装バグを後から選り分けられるようにする
                // The exception type is kept so an implementation bug disguised as unreachability can be told apart later
                var message = $"{exception.GetType().Name}: {exception.GetBaseException().Message}";
                Debug.LogWarning($"[PlaytestReceiver] request to {target} failed: {message}");
                return PlaytestApiResult.TransportFailure(message);
            }
        }

        // ログに出す宛先。署名付きURLのクエリ（X-Amz-Credential・X-Amz-Signature）は1時間有効な権限なので出さない
        // The destination as logged; a presigned URL's query (X-Amz-Credential, X-Amz-Signature) is an hour-long authority, so it is left out
        internal static string DescribeTargetForLog(Uri requestUri)
        {
            return requestUri.GetLeftPart(UriPartial.Path);
        }

        private static HttpClient CreateClient()
        {
            return new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        }
    }
}
