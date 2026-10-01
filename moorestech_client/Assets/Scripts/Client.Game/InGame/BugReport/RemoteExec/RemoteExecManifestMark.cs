using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Client.Game.InGame.BugReport
{
    // 有効/無効/不明の3状態。不明を無効へ落とすと、証跡を読めなかった箱が自動修正へ流れる
    // Three states: enabled, disabled and unknown; folding unknown into disabled would feed unreadable evidence into auto-fix
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RemoteExecManifestMarkState
    {
        Disabled,
        Enabled,
        Unknown,
    }

    // 箱内の台帳パスを取り込み側へ渡す。stateがUnknownならunknownReasonが必ず埋まる
    // Carries bundle-relative ledger paths to the ingest side; an Unknown state always comes with unknownReason
    public sealed class RemoteExecManifestMark
    {
        public RemoteExecManifestMarkState State { get; private set; }
        public string UnknownReason { get; private set; }
        public List<string> LedgerFiles = new();

        private RemoteExecManifestMark(RemoteExecManifestMarkState state, string unknownReason)
        {
            State = state;
            UnknownReason = unknownReason;
        }

        public static RemoteExecManifestMark Disabled()
        {
            return new RemoteExecManifestMark(RemoteExecManifestMarkState.Disabled, null);
        }

        public static RemoteExecManifestMark Enabled()
        {
            return new RemoteExecManifestMark(RemoteExecManifestMarkState.Enabled, null);
        }

        public static RemoteExecManifestMark Unknown(string reason)
        {
            return new RemoteExecManifestMark(RemoteExecManifestMarkState.Unknown, reason);
        }
    }
}
