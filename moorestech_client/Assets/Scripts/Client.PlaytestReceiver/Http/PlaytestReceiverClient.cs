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

        public UniTask<PlaytestApiResult> PostPrepareAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, IReadOnlyList<PlaytestDeclaredFile> files, CancellationToken token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/uploads/{PlaytestUploadPath.ForPrepare(kind, bundleId)}")
            {
                Content = new StringContent(ComposeDeclarationBody(files), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            return SendWithHttpTimeoutAsync(request, token);
        }

        // 宣言のワイヤ表現。path と bytes だけを載せ、AbsolutePath は決して出さない（テストで固定）
        // The wire form of the declaration: only path and bytes, never AbsolutePath (pinned by a test)
        public static string ComposeDeclarationBody(IReadOnlyList<PlaytestDeclaredFile> files)
        {
            var declared = new JArray();
            foreach (var file in files) declared.Add(new JObject { ["path"] = file.Path, ["bytes"] = file.Bytes });
            return new JObject { ["files"] = declared }.ToString(Newtonsoft.Json.Formatting.None);
        }

        // 署名付きURLへの直接PUT。Bearerは付けず（署名が権限）、Content-Lengthは署名に含まれるため必ず明示する
        // The direct PUT to the presigned URL: no bearer (the signature is the authority) and Content-Length is explicit because it is signed
        public async UniTask<PlaytestApiResult> PutToSignedUrlAsync(string signedUrl, string absoluteFilePath, long bytes, CancellationToken token)
        {
            if (!File.Exists(absoluteFilePath))
            {
                Debug.LogWarning($"[PlaytestReceiver] {absoluteFilePath} disappeared before upload");
                return PlaytestApiResult.LocalUnreadableFile($"{absoluteFilePath} does not exist");
            }

            // 手元のディスクI/O境界。開けない・読めない失敗は到達失敗と混ぜずLocalUnreadableFileに分ける
            // Local disk I/O boundary; an unopenable/unreadable failure is kept apart from unreachability as LocalUnreadableFile
            Stream fileStream;
            try
            {
                fileStream = File.OpenRead(absoluteFilePath);
            }
            catch (IOException exception)
            {
                return PlaytestApiResult.LocalUnreadableFile($"{absoluteFilePath}: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                return PlaytestApiResult.LocalUnreadableFile($"{absoluteFilePath}: {exception.Message}");
            }

            using var idle = CancellationTokenSource.CreateLinkedTokenSource(token);
            var idleTimeoutSeconds = PlaytestReceiverConfig.UploadIdleTimeoutSeconds;
            var idleTimeout = TimeSpan.FromSeconds(idleTimeoutSeconds);
            var content = new StreamContent(new IdleTimeoutStream(fileStream, idle, idleTimeout));
            content.Headers.ContentLength = bytes;
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var request = new HttpRequestMessage(HttpMethod.Put, signedUrl) { Content = content };
            return await SendAsync(request, idle, $"no upload progress for {idleTimeoutSeconds}s", token);
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
            var requestUri = request.RequestUri;
            try
            {
                using (request)
                using (var response = await Client.SendAsync(request, deadline.Token))
                {
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
                Debug.LogWarning($"[PlaytestReceiver] request to {requestUri} {cutDescription}");
                return PlaytestApiResult.TransportFailure(cutDescription);
            }
            catch (Exception exception)
            {
                // 例外の型名も残す。到達失敗に見える実装バグを後から選り分けられるようにする
                // The exception type is kept so an implementation bug disguised as unreachability can be told apart later
                var message = $"{exception.GetType().Name}: {exception.GetBaseException().Message}";
                Debug.LogWarning($"[PlaytestReceiver] request to {requestUri} failed: {message}");
                return PlaytestApiResult.TransportFailure(message);
            }
        }

        private static HttpClient CreateClient()
        {
            return new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        }
    }
}
