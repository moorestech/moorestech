using System.Collections.Generic;

namespace Client.RemoteExec.Run
{
    public enum RemoteExecTarget
    {
        Client,
        Server,
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

    // 送信者へ返す実行結果
    // Execution outcome returned to the sender
    public sealed class RemoteExecResult
    {
        public bool Ok;
        public string Result;
        public List<string> CompileErrors = new();
        public string Exception;
        public List<string> Logs = new();

        // RunnerとHTTP境界だけが生成し、失敗時の形を揃える
        // Only the runner and HTTP boundary construct results to keep failures consistent
        internal RemoteExecResult()
        {
        }

        internal static RemoteExecResult FromUnhandledException(System.Exception error)
        {
            return new RemoteExecResult { Exception = error.ToString() };
        }
    }
}
