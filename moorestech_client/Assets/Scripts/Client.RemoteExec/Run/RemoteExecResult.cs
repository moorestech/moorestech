using System.Collections.Generic;

namespace Client.RemoteExec.Run
{
    public enum RemoteExecTarget
    {
        Client,
        Server,
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
    }
}
