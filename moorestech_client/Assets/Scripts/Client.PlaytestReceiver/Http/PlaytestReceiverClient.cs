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
            return SendAsync(request, TimeSpan.FromSeconds(PlaytestReceiverConfig.HttpTimeoutSeconds), token);
        }

        public UniTask<PlaytestApiResult> PutFileAsync(string bearerToken, string kind, string bundleId, string relativePath, string absoluteFilePath, CancellationToken token)
        {
            var uploadPath = PlaytestUploadPath.ForFile(kind, bundleId, relativePath);

            // 逸脱を含む名前は送っても受け口が400を返すだけ。同じ拒否をここで返し、再試行では直らないと呼び出し側へ伝える
            // Such a name would only earn a 400 from the receiver, so the same refusal is returned here: retrying cannot fix it
            if (uploadPath == null)
            {
                Debug.LogError($"[PlaytestReceiver] refused to upload '{relativePath}': it is not a safe relative path");
                return UniTask.FromResult(new PlaytestApiResult { StatusCode = 400, Body = "{\"reason\":\"bad-path\"}" });
            }

            var request = new HttpRequestMessage(HttpMethod.Put, $"{_baseUrl}/v1/uploads/{uploadPath}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            // ファイルを開くのはOS境界。開けないときは送信前に到達失敗へ畳み、呼び出し側の分岐を増やさない
            // Opening the file is an OS boundary; a failure folds into a transport failure before anything is sent
            FileStream stream;
            try
            {
                stream = File.OpenRead(absoluteFilePath);
            }
            catch (Exception exception)
            {
                request.Dispose();
                var message = exception.GetBaseException().Message;
                Debug.LogWarning($"[PlaytestReceiver] could not open {absoluteFilePath}: {message}");
                return UniTask.FromResult(new PlaytestApiResult { TransportError = message });
            }

            var content = new StreamContent(stream);

            // 受け口は Content-Length 必須で、欠落すると411を返す。長さを明示して chunked 送信に落とさない
            // The receiver requires Content-Length and answers 411 without it, so the length is set explicitly
            content.Headers.ContentLength = stream.Length;
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content = content;

            return SendAsync(request, PlaytestReceiverConfig.UploadTimeout(stream.Length), token);
        }

        public UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, string kind, string bundleId, string summaryJson, CancellationToken token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/uploads/{PlaytestUploadPath.ForComplete(kind, bundleId)}")
            {
                Content = new StringContent(summaryJson, Encoding.UTF8, "application/json"),
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

            // ネットワーク送受信は外部境界。到達失敗をTransportErrorへ隔離し、呼び出し側は状態コードだけを見る
            // Network I/O is an external boundary; unreachability is isolated into TransportError for the caller
            try
            {
                using (request)
                using (var response = await Client.SendAsync(request, deadline.Token))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    return new PlaytestApiResult { StatusCode = (int)response.StatusCode, Body = body };
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
                return new PlaytestApiResult { TransportError = message };
            }
            catch (Exception exception)
            {
                // 例外の型名も残す。到達失敗に見える実装バグを後から選り分けられるようにする
                // The exception type is kept so an implementation bug disguised as unreachability can be told apart later
                var message = $"{exception.GetType().Name}: {exception.GetBaseException().Message}";
                Debug.LogWarning($"[PlaytestReceiver] request to {requestUri} failed: {message}");
                return new PlaytestApiResult { TransportError = message };
            }
        }

        private static HttpClient CreateClient()
        {
            return new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        }
    }
}
