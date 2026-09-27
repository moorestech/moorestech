using System;
using System.IO;
using System.Threading.Tasks;
using Client.RemoteExec.Access;
using Client.RemoteExec.Run;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Client.RemoteExec
{
    public static class RemoteExecEndpoint
    {
        public const string Path = "/api/remote-exec";
        private static readonly JsonSerializerSettings ResponseSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

        public static async Task HandleAsync(HttpContext context)
        {
            if (!RemoteExecLaunchOption.IsEnabled)
            {
                await RejectAsync(context, 404, "起動オプションが無いため遠隔実行は無効です");
                return;
            }
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
            if (targetName != "client" && targetName != "server")
            {
                await RejectAsync(context, 400, $"未知の実行先です: {targetName}");
                return;
            }

            // 検証した要求を実行し、成功・失敗の両方を台帳へ残す
            // Execute validated input and record both success and failure
            var code = (string)codeValue;
            var target = targetName == "server" ? RemoteExecTarget.Server : RemoteExecTarget.Client;
            var result = await RemoteExecRunner.RunAsync(code, target);
            RemoteExecLedger.Append(targetName, code, result.Ok);
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonConvert.SerializeObject(result, ResponseSettings), context.RequestAborted);
        }

        private static bool IsAuthorized(HttpContext context, out string reason)
        {
            reason = null;
            if (context.Request.Method != "POST") reason = "POST 以外";
            else if (context.Request.Headers.ContainsKey("Origin")) reason = "Origin ヘッダ付き（ブラウザ由来）";
            else if (RemoteExecAccessFile.Token == null) reason = "トークン未発行";
            else if (context.Request.Headers[RemoteExecAccessFile.HeaderName].ToString() != RemoteExecAccessFile.Token) reason = "トークン不一致";
            return reason == null;
        }

        private static async Task RejectAsync(HttpContext context, int status, string reason)
        {
            Debug.LogWarning($"[RemoteExec] 要求を拒否しました: {reason}");
            context.Response.StatusCode = status;
            await context.Response.WriteAsync(reason, context.RequestAborted);
        }
    }
}
