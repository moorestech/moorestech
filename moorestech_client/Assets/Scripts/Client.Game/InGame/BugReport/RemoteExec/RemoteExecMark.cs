using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Client.Game.InGame.BugReport
{
    // 有効/無効/不明の3状態。不明を無効へ落とすと、証跡を読めなかった箱が自動修正へ流れる
    // Three states: enabled, disabled and unknown; folding unknown into disabled would feed unreadable evidence into auto-fix
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RemoteExecMarkState
    {
        Disabled,
        Enabled,
        Unknown,
    }

    // 箱内の台帳パスを取り込み側へ渡す。stateがUnknownならunknownReasonが必ず埋まる
    // Carries bundle-relative ledger paths to the ingest side; an Unknown state always comes with unknownReason
    public sealed class RemoteExecMark
    {
        public RemoteExecMarkState State { get; private set; }
        public string UnknownReason { get; private set; }
        public List<string> LedgerFiles = new();

        private RemoteExecMark(RemoteExecMarkState state, string unknownReason)
        {
            State = state;
            UnknownReason = unknownReason;
        }

        public static RemoteExecMark Disabled()
        {
            return new RemoteExecMark(RemoteExecMarkState.Disabled, null);
        }

        public static RemoteExecMark Enabled()
        {
            return new RemoteExecMark(RemoteExecMarkState.Enabled, null);
        }

        public static RemoteExecMark Unknown(string reason)
        {
            return new RemoteExecMark(RemoteExecMarkState.Unknown, reason);
        }
    }
}
