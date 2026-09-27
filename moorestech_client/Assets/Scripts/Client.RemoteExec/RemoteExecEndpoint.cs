using System;
using System.IO;
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
            // 登録時の有効判定はWebUiEndpointsが担い、ここでは各要求を認証する
            // WebUiEndpoints checks boot activation when routing; this method authenticates each request
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

            // 実行前にソースを記録し、クラッシュや停止でも開始行を残す
            // Record source before running so a crash or hang still leaves a start entry
            var code = (string)codeValue;
            long? sequence = null;
            RemoteExecResult result;
            // HTTP要求の実行境界で想定外の例外を隔離し、Kestrelの無音500を防ぐ
            // Isolate unexpected exceptions at the HTTP request boundary instead of a silent Kestrel 500
            try
            {
                sequence = RemoteExecLedger.AppendStart(target, code);
                result = await runner.RunAsync(code, target);
            }
            catch (Exception error)
            {
                Debug.LogError($"[RemoteExec] 実行要求に失敗しました: {error}");
                result = RemoteExecResult.FromUnhandledException(error);
            }
            if (sequence.HasValue) RemoteExecLedger.AppendResult(sequence.Value, result.Ok);
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonConvert.SerializeObject(result, ResponseSettings), context.RequestAborted);

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
                await ctx.Response.WriteAsync(rejectReason, ctx.RequestAborted);
            }

            #endregion
        }
    }

    internal interface IRemoteExecHttpRunner
    {
        UniTask<RemoteExecResult> RunAsync(string body, RemoteExecTarget target);
    }

    internal sealed class RemoteExecHttpRunner : IRemoteExecHttpRunner
    {
        internal static readonly RemoteExecHttpRunner Instance = new();

        public UniTask<RemoteExecResult> RunAsync(string body, RemoteExecTarget target)
        {
            return RemoteExecRunner.RunAsync(body, target);
        }
    }
}
