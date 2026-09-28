using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Client.RemoteExec.Run
{
    public enum RemoteExecTarget
    {
        Client,
        Server,
    }

    // 拒否は原因ごとに分ける。1つのRejectedでは取り消しと権限不足と入口拒否が同じ結果に潰れる
    // Rejections split by cause; one Rejected would flatten cancellation, missing authority and entry refusal into one outcome
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RemoteExecOutcome
    {
        Cancelled,
        ServerUnavailable,
        Unauthorized,
        BadRequest,
        CompileFailed,
        RuntimeException,
        Succeeded,
    }

    // RemoteExecTargetのワイヤ文字列との対応をここ1箇所に置く
    // The single place mapping RemoteExecTarget to and from its wire string
    public static class RemoteExecTargetWireName
    {
        public static bool TryParse(string wireName, out RemoteExecTarget target)
        {
            switch (wireName)
            {
                case "client":
                    target = RemoteExecTarget.Client;
                    return true;
                case "server":
                    target = RemoteExecTarget.Server;
                    return true;
                default:
                    target = default;
                    return false;
            }
        }

        public static string ToWireName(RemoteExecTarget target)
        {
            switch (target)
            {
                case RemoteExecTarget.Client: return "client";
                case RemoteExecTarget.Server: return "server";
                default: throw new System.ArgumentOutOfRangeException(nameof(target), target, "未知の実行先です");
            }
        }
    }

    // 送信者へ返す実行結果。生成はstatic factoryだけに限り、結果と拒否理由を混ぜない
    // Execution outcome returned to the sender; only the static factories build it, keeping results and rejection reasons apart
    public sealed class RemoteExecResult
    {
        public RemoteExecOutcome Outcome { get; }
        public string Result { get; }
        public IReadOnlyList<string> CompileErrors { get; }

        // 入口や停止で拒否した理由。送信コードが投げた例外（Exception）とは別の列に出す
        // Why the entry or the shutdown refused the request; kept in a different column from the submitted code's exception
        public string RejectionReason { get; }
        public string Exception { get; }
        public IReadOnlyList<string> Logs { get; }

        private RemoteExecResult(RemoteExecOutcome outcome, string result, IReadOnlyList<string> compileErrors,
            string rejectionReason, string exception, IReadOnlyList<string> logs)
        {
            Outcome = outcome;
            Result = result;
            CompileErrors = compileErrors ?? EmptyLines;
            RejectionReason = rejectionReason;
            Exception = exception;
            Logs = logs ?? EmptyLines;
        }

        private static readonly string[] EmptyLines = new string[0];

        public static RemoteExecResult Succeeded(string result, IReadOnlyList<string> logs)
        {
            return new RemoteExecResult(RemoteExecOutcome.Succeeded, result, null, null, null, logs);
        }

        public static RemoteExecResult CompileFailed(IReadOnlyList<string> errors, IReadOnlyList<string> logs)
        {
            return new RemoteExecResult(RemoteExecOutcome.CompileFailed, null, errors, null, null, logs);
        }

        public static RemoteExecResult RuntimeException(string exception, IReadOnlyList<string> logs)
        {
            return new RemoteExecResult(RemoteExecOutcome.RuntimeException, null, null, null, exception, logs);
        }

        public static RemoteExecResult Cancelled(string reason, IReadOnlyList<string> logs)
        {
            return new RemoteExecResult(RemoteExecOutcome.Cancelled, null, null, reason, null, logs);
        }

        public static RemoteExecResult ServerUnavailable(string reason, IReadOnlyList<string> logs)
        {
            return new RemoteExecResult(RemoteExecOutcome.ServerUnavailable, null, null, reason, null, logs);
        }

        public static RemoteExecResult Unauthorized(string reason)
        {
            return new RemoteExecResult(RemoteExecOutcome.Unauthorized, null, null, reason, null, null);
        }

        public static RemoteExecResult BadRequest(string reason)
        {
            return new RemoteExecResult(RemoteExecOutcome.BadRequest, null, null, reason, null, null);
        }
    }
}
