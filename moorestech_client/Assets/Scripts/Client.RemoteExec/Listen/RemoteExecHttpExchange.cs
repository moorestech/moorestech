using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Client.RemoteExec.Access;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.RemoteExec.Listen
{
    // - HTTP要求をDTO化
    // - 応答をHTTPへ書き戻す
    // - Convert HTTP requests to DTOs
    // - Write responses back over HTTP
    internal static class RemoteExecHttpExchange
    {
        internal static async UniTask HandleAsync(HttpListenerContext context, CancellationToken lifetime)
        {
            // ヘッダは値が空でも「付いている」とみなす（空のOriginもブラウザ由来として拒否する）
            // A header counts as present even when empty, so an empty Origin is still refused as browser-originated
            var source = context.Request;
            var request = new RemoteExecHttpRequest(source.Url.AbsolutePath, source.HttpMethod,
                source.Headers.Get("Origin") != null, source.Headers.Get(RemoteExecAccessFile.HeaderName), source.InputStream);
            var response = await RemoteExecEndpoint.HandleAsync(request, lifetime);
            var bytes = Encoding.UTF8.GetBytes(response.SerializeBody());

            // HTTP送信境界の切断を記録し、実行結果が未達だったことを残す
            // Record disconnects at the HTTP send boundary so undelivered results remain visible
            try
            {
                context.Response.StatusCode = response.StatusCode;
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length, lifetime);
                context.Response.Close();
            }
            catch (Exception e) when (e is IOException || e is HttpListenerException || e is ObjectDisposedException || e is OperationCanceledException)
            {
                Debug.LogWarning($"[RemoteExec] 応答を届けられませんでした: {e.Message}");
                context.Response.Abort();
            }
        }
    }
}
