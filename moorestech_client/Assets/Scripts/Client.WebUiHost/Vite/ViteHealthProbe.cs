using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Client.WebUiHost.Vite
{
    /// <summary>
    /// ViteのHTTP応答を期限付きで確認する
    /// Checks Vite HTTP responsiveness with a deadline
    /// </summary>
    public static class ViteHealthProbe
    {
        private static readonly HttpClient Client = new();

        // exitToken による中断は「不健康」ではなく呼び出し側の打ち切りなので、falseに畳まず伝播させる
        // Cancellation via exitToken is the caller aborting, not ill health, so it propagates instead of folding into false
        public static async Task<bool> IsHealthyAsync(int port, TimeSpan timeout, CancellationToken exitToken)
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(exitToken);
            cancellation.CancelAfter(timeout);

            // 外部HTTP障害をfalseへ隔離する
            // HTTP crosses an external-process boundary, so isolate connection failures and timeouts as false
            try
            {
                using var response = await Client.GetAsync($"http://127.0.0.1:{port}/", cancellation.Token);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (OperationCanceledException) when (!exitToken.IsCancellationRequested)
            {
                return false;
            }
        }
    }
}
