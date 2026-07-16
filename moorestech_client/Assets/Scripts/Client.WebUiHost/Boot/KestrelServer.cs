using System;
using System.IO;
using System.Threading.Tasks;
using Client.WebUiHost.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// Kestrel IWebHost の起動/停止を包むラッパ。ベースポートから空きを探索して bind する
    /// Wrapper around Kestrel IWebHost lifecycle; probes upward from the base port for a free one
    /// </summary>
    public class KestrelServer
    {
        private IWebHost _webHost;

        // 起動時に確定した実ポート。未起動時は 0
        // Actual port resolved at startup; 0 before start
        public int ActualPort => _actualPort;
        private int _actualPort;

        public async Task StartAsync(WebSocketHub hub)
        {
            // ベースから 1 ずつ上げながら bind を試行し、最初に成功したポートを採用する
            // Probe upward from the base port and adopt the first successful bind
            for (var port = WebUiPortConfig.KestrelBasePort; port < WebUiPortConfig.KestrelBasePort + WebUiPortConfig.PortSearchRange; port++)
            {
                var url = $"http://127.0.0.1:{port}";
                var webHost = new WebHostBuilder()
                    .UseKestrel()
                    .UseUrls(url)
                    .ConfigureServices(services => services.AddRouting())
                    .Configure(app => WebUiEndpoints.Configure(app, hub))
                    .Build();

                // ポート使用中の bind 失敗は IOException で通知される。OS ネットワーク境界の隔離のためここに限り try-catch を使用
                // A bind on an occupied port surfaces as IOException; try-catch here only isolates the OS network boundary
                try
                {
                    await webHost.StartAsync();
                }
                catch (IOException)
                {
                    webHost.Dispose();
                    continue;
                }

                _webHost = webHost;
                _actualPort = port;
                Debug.Log($"[WebUiHost] Kestrel started at {url}");
                return;
            }

            throw new InvalidOperationException(
                $"[WebUiHost] no free port in {WebUiPortConfig.KestrelBasePort}..{WebUiPortConfig.KestrelBasePort + WebUiPortConfig.PortSearchRange - 1}");
        }

        public async Task StopAsync()
        {
            if (_webHost == null) return;

            // 最大 2 秒で graceful shutdown
            // Graceful shutdown capped at 2 seconds
            await _webHost.StopAsync(TimeSpan.FromSeconds(2));
            _webHost.Dispose();
            _webHost = null;
            _actualPort = 0;
            Debug.Log("[WebUiHost] Kestrel stopped");
        }
    }
}
