using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Client.RemoteExec.Access;
using Client.RemoteExec.Run;
using Cysharp.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Client.RemoteExec
{
    // 認証済みのHTTP要求を実行し、結果と実行履歴を返す
    // Run authenticated HTTP requests and return their results and execution history
    public static class RemoteExecEndpoint
    {
        public const string Path = "/api/remote-exec";
        private static readonly JsonSerializerSettings ResponseSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

        public static Task HandleAsync(HttpContext context)
        {
            return HandleAsync(context, RemoteExecHttpRunner.Instance);
        }

        internal static async Task HandleAsync(HttpContext context, IRemoteExecHttpRunner runner)
        {
            // 無効な起動では経路を隠し、理由を開発者ログへ残す
            // Hide the route on disabled boots and log the reason for developers
            if (!RemoteExecLaunchOption.IsEnabled)
            {
                Debug.LogWarning("[RemoteExec] 起動オプションが無いため要求を404で拒否しました");
                context.Response.StatusCode = 404;
                await WriteResponseAsync(context, "not found");
                return;
            }

            // 有効な起動では各要求を認証する
            // Authenticate each request on enabled boots
            if (!IsAuthorized(context, out var reason))
            {
                await RejectAsync(context, 403, reason);
                return;
            }

            // 外部入力のJSON・ストリーム境界で読み取り失敗を隔離する
            // Isolate read failures at the external JSON and network stream boundary
            JObject request;
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                request = JObject.Parse(await reader.ReadToEndAsync());
            }
            catch (Exception e) when (e is JsonException || e is IOException)
            {
                await RejectAsync(context, 400, $"要求本文を読めませんでした: {e.Message}");
                return;
            }

            var codeValue = request["code"];
            var targetValue = request["target"];
            if (codeValue?.Type != JTokenType.String || targetValue?.Type != JTokenType.String)
            {
                await RejectAsync(context, 400, "code と target は文字列で指定してください");
                return;
            }
            var targetName = (string)targetValue;
            if (!RemoteExecTargetWireName.TryParse(targetName, out var target))
            {
                await RejectAsync(context, 400, $"未知の実行先です: {targetName}");
                return;
            }

            var code = (string)codeValue;
            var result = await RemoteExecRunner.RunAsync(code, target, context.RequestAborted, runner);
            context.Response.ContentType = "application/json; charset=utf-8";
            await WriteResponseAsync(context, JsonConvert.SerializeObject(result, ResponseSettings));

            #region Internal

            bool IsAuthorized(HttpContext ctx, out string authReason)
            {
                authReason = null;
                if (ctx.Request.Method != "POST") authReason = "POST 以外";
                else if (ctx.Request.Headers.ContainsKey("Origin")) authReason = "Origin ヘッダ付き（ブラウザ由来）";
                else if (RemoteExecAccessFile.Token == null) authReason = "トークン未発行";
                else if (ctx.Request.Headers[RemoteExecAccessFile.HeaderName].ToString() != RemoteExecAccessFile.Token) authReason = "トークン不一致";
                return authReason == null;
            }

            async Task RejectAsync(HttpContext ctx, int status, string rejectReason)
            {
                Debug.LogWarning($"[RemoteExec] 要求を拒否しました: {rejectReason}");
                ctx.Response.StatusCode = status;
                await WriteResponseAsync(ctx, rejectReason);
            }

            async Task WriteResponseAsync(HttpContext ctx, string text)
            {
                // HTTP送信境界の切断を記録し、実行結果が未達だったことを残す
                // Record disconnects at the HTTP send boundary so undelivered results remain visible
                try { await ctx.Response.WriteAsync(text, ctx.RequestAborted); }
                catch (Exception error) when (error is IOException || error is OperationCanceledException)
                {
                    Debug.LogWarning($"[RemoteExec] 応答を届けられませんでした: {error.Message}");
                }
            }

            #endregion
        }
    }

    internal interface IRemoteExecHttpRunner
    {
        UniTask<RemoteExecResult> RunAsync(string body, RemoteExecTarget target, CancellationToken cancellationToken);
    }

    internal sealed class RemoteExecHttpRunner : IRemoteExecHttpRunner
    {
        internal static readonly RemoteExecHttpRunner Instance = new();

        public UniTask<RemoteExecResult> RunAsync(string body, RemoteExecTarget target, CancellationToken cancellationToken)
        {
            return RemoteExecRunner.ExecuteAsync(body, target, cancellationToken);
        }
    }
}
