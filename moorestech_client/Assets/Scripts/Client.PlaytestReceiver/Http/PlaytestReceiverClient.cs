using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
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
            return SendAsync(request, token);
        }

        public UniTask<PlaytestApiResult> PutFileAsync(string bearerToken, string kind, string bundleId, string relativePath, string absoluteFilePath, CancellationToken token)
        {
            // ファイルを開くのはOS境界。開けないときは送信前に到達失敗へ畳み、呼び出し側の分岐を増やさない
            // Opening the file is an OS boundary; a failure folds into a transport failure before anything is sent
            FileStream stream;
            try
            {
                stream = File.OpenRead(absoluteFilePath);
            }
            catch (Exception exception)
            {
                var message = exception.GetBaseException().Message;
                Debug.LogWarning($"[PlaytestReceiver] could not open {absoluteFilePath}: {message}");
                return UniTask.FromResult(new PlaytestApiResult { TransportError = message });
            }

            var content = new StreamContent(stream);

            // 受け口は Content-Length 必須で、欠落すると411を返す。長さを明示して chunked 送信に落とさない
            // The receiver requires Content-Length and answers 411 without it, so the length is set explicitly
            content.Headers.ContentLength = stream.Length;
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var request = new HttpRequestMessage(HttpMethod.Put, $"{_baseUrl}/v1/uploads/{kind}/{bundleId}/{relativePath}")
            {
                Content = content,
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            return SendAsync(request, token);
        }

        public UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, string kind, string bundleId, string summaryJson, CancellationToken token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/uploads/{kind}/{bundleId}/complete")
            {
                Content = new StringContent(summaryJson, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            return SendAsync(request, token);
        }

        private static async UniTask<PlaytestApiResult> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var requestUri = request.RequestUri;

            // ネットワーク送受信は外部境界。到達失敗をTransportErrorへ隔離し、呼び出し側は状態コードだけを見る
            // Network I/O is an external boundary; unreachability is isolated into TransportError for the caller
            try
            {
                using (request)
                using (var response = await Client.SendAsync(request, token))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    return new PlaytestApiResult { StatusCode = (int)response.StatusCode, Body = body };
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var message = exception.GetBaseException().Message;
                Debug.LogWarning($"[PlaytestReceiver] request to {requestUri} failed: {message}");
                return new PlaytestApiResult { TransportError = message };
            }
        }

        private static HttpClient CreateClient()
        {
            return new HttpClient { Timeout = TimeSpan.FromSeconds(PlaytestReceiverConfig.HttpTimeoutSeconds) };
        }
    }
}
