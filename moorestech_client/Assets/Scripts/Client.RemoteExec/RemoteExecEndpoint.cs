using System;
using System.IO;
using System.Threading;
using Client.RemoteExec.Access;
using Client.RemoteExec.Run;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.RemoteExec
{
    // 認証済みの要求を実行し、結果を応答の形で返す。待受の種類は知らない
    // Run authenticated requests and return the result as a response, knowing nothing of the listener kind
    internal static class RemoteExecEndpoint
    {
        internal const string Path = "/api/remote-exec";

        internal static async UniTask<RemoteExecHttpResponse> HandleAsync(RemoteExecHttpRequest request, CancellationToken cancellationToken)
        {
            // 専用待受は遠隔実行だけを受ける。他の経路は理由付きで隠す
            // The dedicated listener serves remote exec only; any other route is hidden with a reason
            if (request.Path != Path) return Reject(404, RemoteExecResult.BadRequest($"未知の経路です: {request.Path}"));

            // 無効な起動では経路を隠し、理由を開発者ログへ残す
            // Hide the route on disabled boots and log the reason for developers
            if (!RemoteExecLaunchOption.IsEnabled) return Reject(404, RemoteExecResult.Unauthorized("起動オプションが無いため要求を404で拒否しました"));

            // 有効な起動では各要求を認証する
            // Authenticate each request on enabled boots
            if (!IsAuthorized(out var reason)) return Reject(403, RemoteExecResult.Unauthorized(reason));

            // 外部入力のJSON・ストリーム境界で読み取り失敗を隔離する
            // Isolate read failures at the external JSON and network stream boundary
            JObject body;
            try
            {
                using var reader = new StreamReader(request.Body);
                body = JObject.Parse(await reader.ReadToEndAsync());
            }
            catch (Exception e) when (e is JsonException || e is IOException)
            {
                return Reject(400, RemoteExecResult.BadRequest($"要求本文を読めませんでした: {e.Message}"));
            }

            var codeValue = body["code"];
            var targetValue = body["target"];
            if (codeValue?.Type != JTokenType.String || targetValue?.Type != JTokenType.String)
                return Reject(400, RemoteExecResult.BadRequest("code と target は文字列で指定してください"));
            var targetName = (string)targetValue;
            if (!RemoteExecTargetWireName.TryParse(targetName, out var target))
                return Reject(400, RemoteExecResult.BadRequest($"未知の実行先です: {targetName}"));

            var result = await RemoteExecRunner.RunAsync((string)codeValue, target, cancellationToken);
            return new RemoteExecHttpResponse(200, result);

            #region Internal

            bool IsAuthorized(out string authReason)
            {
                authReason = null;
                if (request.Method != "POST") authReason = "POST 以外";
                else if (request.HasOriginHeader) authReason = "Origin ヘッダ付き（ブラウザ由来）";
                else if (RemoteExecAccessFile.Token == null) authReason = "トークン未発行";
                else if (request.TokenHeader != RemoteExecAccessFile.Token) authReason = "トークン不一致";
                return authReason == null;
            }

            // 入口の拒否も実行結果と同じ形で返す。送信側は成功も拒否も1つの型として読める
            // Entry rejections come back in the same shape as a result, so the sender reads success and refusal through one type
            static RemoteExecHttpResponse Reject(int status, RemoteExecResult rejection)
            {
                Debug.LogWarning($"[RemoteExec] 要求を拒否しました: {rejection.RejectionReason}");
                return new RemoteExecHttpResponse(status, rejection);
            }

            #endregion
        }
    }
}
