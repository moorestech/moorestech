using System;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

                    // IHttpUpgradeFeature で TCP ストリームを取得し手動で WS ハンドシェイク実行
                    // Get raw TCP stream via IHttpUpgradeFeature and perform WS handshake manually
                    var upgradeFeature = context.Features.Get<IHttpUpgradeFeature>();
                    if (upgradeFeature == null || !upgradeFeature.IsUpgradableRequest)
                    {
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsync("not an upgradable request", CancellationToken.None);
                        return;
                    }

                    var key = context.Request.Headers["Sec-WebSocket-Key"].ToString();
                    if (string.IsNullOrEmpty(key))
                    {
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsync("missing Sec-WebSocket-Key", CancellationToken.None);
                        return;
                    }

                    // RFC 6455 に従い Sec-WebSocket-Accept を計算
                    // Compute Sec-WebSocket-Accept per RFC 6455
                    var accept = ComputeAcceptKey(key);

                    // 101 Switching Protocols レスポンスを書き込み
                    // Write 101 Switching Protocols response
                    context.Response.StatusCode = 101;
                    context.Response.Headers["Upgrade"] = "websocket";
                    context.Response.Headers["Connection"] = "Upgrade";
                    context.Response.Headers["Sec-WebSocket-Accept"] = accept;

                    // アップグレードして生 Stream を取得
                    // Upgrade and get raw Stream
                    var stream = await upgradeFeature.UpgradeAsync();

                    // System.Net.WebSockets.WebSocket をサーバ側として生成
                    // Create server-side WebSocket from the raw stream
                    var ws = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, keepAliveInterval: TimeSpan.FromSeconds(30));
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

        // RFC 6455 の WS ハンドシェイクキーを計算する
        // Compute WebSocket handshake accept key per RFC 6455
        private static string ComputeAcceptKey(string key)
        {
            const string guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            var combined = key + guid;
            var bytes = Encoding.UTF8.GetBytes(combined);
            var hash = SHA1.Create().ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private static bool IsAllowedOrigin(string origin)
        {
            if (string.IsNullOrEmpty(origin)) return false;
            return origin == "http://localhost:5173" || origin == "http://127.0.0.1:5173";
        }
    }
}
