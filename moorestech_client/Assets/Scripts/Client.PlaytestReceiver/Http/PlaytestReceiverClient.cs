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
            return SendAsync(request, TimeSpan.FromSeconds(PlaytestReceiverConfig.HttpTimeoutSeconds), token);
        }

        public UniTask<PlaytestApiResult> PostPrepareAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, IReadOnlyList<PlaytestDeclaredFile> files, CancellationToken token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/uploads/{PlaytestUploadPath.ForPrepare(kind, bundleId)}")
            {
                Content = new StringContent(ComposeDeclarationBody(files), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            return SendAsync(request, TimeSpan.FromSeconds(PlaytestReceiverConfig.HttpTimeoutSeconds), token);
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
        public UniTask<PlaytestApiResult> PutToSignedUrlAsync(string signedUrl, string absoluteFilePath, long bytes, CancellationToken token)
        {
            if (!File.Exists(absoluteFilePath))
            {
                Debug.LogWarning($"[PlaytestReceiver] {absoluteFilePath} disappeared before upload");
                return UniTask.FromResult(PlaytestApiResult.LocalUnreadableFile($"{absoluteFilePath} does not exist"));
            }
            return SendWithIdleTimeoutAsync(signedUrl, absoluteFilePath, bytes, token);
        }

        private static async UniTask<PlaytestApiResult> SendWithIdleTimeoutAsync(string signedUrl, string absoluteFilePath, long bytes, CancellationToken token)
        {
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(token);
            var idleTimeout = TimeSpan.FromSeconds(PlaytestReceiverConfig.UploadIdleTimeoutSeconds);
            var content = new StreamContent(new IdleTimeoutStream(File.OpenRead(absoluteFilePath), idle, idleTimeout));
            content.Headers.ContentLength = bytes;
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var request = new HttpRequestMessage(HttpMethod.Put, signedUrl) { Content = content };
            // ネットワーク送受信は外部境界。到達失敗とアイドル切れをTransportFailureへ隔離する
            // Network I/O is an external boundary; unreachability and an idle cut are isolated into TransportFailure
            try
            {
                using (request)
                using (var response = await Client.SendAsync(request, idle.Token))
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
                var message = $"no upload progress for {idleTimeout.TotalSeconds:0}s";
                Debug.LogWarning($"[PlaytestReceiver] PUT to R2 for {absoluteFilePath} {message}");
                return PlaytestApiResult.TransportFailure(message);
            }
            catch (Exception exception)
            {
                var message = $"{exception.GetType().Name}: {exception.GetBaseException().Message}";
                Debug.LogWarning($"[PlaytestReceiver] PUT to R2 for {absoluteFilePath} failed: {message}");
                return PlaytestApiResult.TransportFailure(message);
            }
        }

        public UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, string supplementJson, CancellationToken token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/uploads/{PlaytestUploadPath.ForComplete(kind, bundleId)}")
            {
                Content = new StringContent(supplementJson, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            return SendAsync(request, TimeSpan.FromSeconds(PlaytestReceiverConfig.HttpTimeoutSeconds), token);
        }

        // 期限は呼び出しごとに与える。HttpClient.Timeoutは本文送信を含む全体に効き、大きい箱を回線速度で殺すため使わない
        // The deadline is per call; HttpClient.Timeout covers body upload too and would kill big bundles on slow lines
        private static async UniTask<PlaytestApiResult> SendAsync(HttpRequestMessage request, TimeSpan timeout, CancellationToken token)
        {
            var requestUri = request.RequestUri;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(timeout);

            // ネットワーク送受信は外部境界。到達失敗をTransportFailureへ隔離し、呼び出し側は種別と状態コードだけを見る
            // Network I/O is an external boundary; unreachability is isolated into TransportFailure for the caller
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
                var message = $"timed out after {timeout.TotalSeconds:0}s";
                Debug.LogWarning($"[PlaytestReceiver] request to {requestUri} {message}");
                return PlaytestApiResult.TransportFailure(message);
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
