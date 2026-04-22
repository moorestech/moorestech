using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// Kestrel のルーティング設定。/api と /ws を提供
    /// Kestrel routing: provides /api and /ws
    /// </summary>
    public static class WebUiEndpoints
    {
        public static void Configure(IApplicationBuilder app, WebSocketHub hub)
        {
            app.Run(async context =>
            {
                var path = context.Request.Path.Value ?? "";

                if (path == "/ws")
                {
                    // Origin ヘッダを検査
                    // Validate Origin header
                    if (!IsAllowedOrigin(context.Request.Headers["Origin"].ToString()))
                    {
                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsync("forbidden origin", CancellationToken.None);
                        return;
                    }

                    // IHttpWebSocketFeature で WebSocket アップグレードを実行
                    // Perform WebSocket upgrade via IHttpWebSocketFeature
                    var wsFeature = context.Features.Get<IHttpWebSocketFeature>();
                    if (wsFeature == null || !wsFeature.IsWebSocketRequest)
                    {
                        context.Response.StatusCode = 400;
                        return;
                    }
                    var ws = await wsFeature.AcceptAsync(null);
                    await hub.HandleConnectionAsync(ws, context.RequestAborted);
                    return;
                }

                if (path == "/api/ping")
                {
                    // ヘルスチェック兼疎通確認用エンドポイント
                    // Health / connectivity check
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsync("{\"ok\":true}", CancellationToken.None);
                    return;
                }

                // 上記以外は 404 を返す
                // Return 404 for all other paths
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("not found", CancellationToken.None);
            });
        }

        private static bool IsAllowedOrigin(string origin)
        {
            if (string.IsNullOrEmpty(origin)) return false;
            return origin == "http://localhost:5173" || origin == "http://127.0.0.1:5173";
        }
    }
}
