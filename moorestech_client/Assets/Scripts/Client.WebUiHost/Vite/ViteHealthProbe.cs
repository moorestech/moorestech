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

        public static async Task<bool> IsHealthyAsync(int port, TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);

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
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
