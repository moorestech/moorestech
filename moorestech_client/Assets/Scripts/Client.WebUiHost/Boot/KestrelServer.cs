using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// Kestrel IWebHost の起動/停止を包むラッパ
    /// Wrapper around Kestrel IWebHost lifecycle
    /// </summary>
    public class KestrelServer
    {
        private const int Port = 5050;
        private IWebHost _webHost;

        public async Task StartAsync(WebSocketHub hub)
        {
            var url = $"http://127.0.0.1:{Port}";

            // WebHostBuilder でルーティングと WebSocket エンドポイントを構成
            // Configure routing and WebSocket endpoints via WebHostBuilder
            _webHost = new WebHostBuilder()
                .UseKestrel()
                .UseUrls(url)
                .ConfigureServices(services => services.AddRouting())
                .Configure(app => WebUiEndpoints.Configure(app, hub))
                .Build();

            await _webHost.StartAsync();
            Debug.Log($"[WebUiHost] Kestrel started at {url}");
        }

        public async Task StopAsync()
        {
            if (_webHost == null) return;

            // 最大 2 秒で graceful shutdown
            // Graceful shutdown capped at 2 seconds
            await _webHost.StopAsync(TimeSpan.FromSeconds(2));
            _webHost.Dispose();
            _webHost = null;
            Debug.Log("[WebUiHost] Kestrel stopped");
        }
    }
}
