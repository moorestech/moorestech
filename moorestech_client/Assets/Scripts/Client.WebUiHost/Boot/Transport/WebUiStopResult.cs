using System.Collections.Generic;

namespace Client.WebUiHost.Boot.Transport
{
    // Socketとプロセス/リスナーの停止失敗を別々に保持する
    // Keep socket termination and process/listener shutdown failures distinct
    public sealed class WebUiStopResult
    {
        public readonly WebSocketCloseResult Connections;
        public readonly IReadOnlyDictionary<string, string> ResourceFailures;
        public bool Succeeded => Connections.Succeeded && ResourceFailures.Count == 0;

        internal WebUiStopResult(WebSocketCloseResult connections, Dictionary<string, string> resourceFailures)
        {
            Connections = connections;
            ResourceFailures = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(resourceFailures);
        }
    }
}
