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

            // HTTP送信境界の切断を記録し、待受停止以外の未達は失敗として呼び出し側へ返す
            // Record disconnects at the HTTP send boundary and surface non-stop delivery failures to the caller
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
                context.Response.Abort();
                if (lifetime.IsCancellationRequested)
                {
                    // 待受の停止による中断は異常でないため失敗として返さない
                    // An interruption caused by stopping the listener is not a fault, so it is not surfaced as a failure
                    Debug.LogWarning($"[RemoteExec] 待受の停止により応答を中断しました: {e.Message}");
                    return;
                }
                Debug.LogWarning($"[RemoteExec] 応答を届けられませんでした: {e.Message}");
                throw;
            }
        }
    }
}
