using System;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Client.WebUiHost
{
    // CEF Web UI 用のローカル HTTP サーバー起動テスト
    // Local HTTP server boot test for the CEF Web UI
    public class WebUiHostBehaviour : MonoBehaviour
    {
        [SerializeField] private int port = 5050;

        private IWebHost _webHost;

        private void Start()
        {
            StartWebHost();
        }

        private void OnDestroy()
        {
            StopWebHost();
        }

        #region Internal

        private void StartWebHost()
        {
            // Kestrel を指定ポートで起動し、ルートだけ返す最小構成
            // Minimal Kestrel host bound to the configured port, serving the root route only
            var url = $"http://127.0.0.1:{port}";

            _webHost = new WebHostBuilder()
                .UseKestrel()
                .UseUrls(url)
                .ConfigureServices(services => services.AddRouting())
                .Configure(Configure)
                .Build();

            _webHost.Start();
            Debug.Log($"[WebUiHost] started at {url}");
        }

        private void Configure(IApplicationBuilder app)
        {
            // とりあえずのレスポンス。アイコン配信等は後段で差し替える
            // Placeholder response; asset routes (icons, etc.) will be wired later
            app.Run(async context =>
            {
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync("moorestech WebUiHost OK", CancellationToken.None);
            });
        }

        private void StopWebHost()
        {
            if (_webHost == null) return;

            // シャットダウン完了まで最大 2 秒待機
            // Wait up to 2 seconds for graceful shutdown
            try
            {
                _webHost.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            }
            finally
            {
                _webHost.Dispose();
                _webHost = null;
            }

            Debug.Log("[WebUiHost] stopped");
        }

        #endregion
    }
}
