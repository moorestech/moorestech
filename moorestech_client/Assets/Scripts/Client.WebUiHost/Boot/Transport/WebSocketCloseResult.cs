using System;
using System.Collections.Generic;

namespace Client.WebUiHost.Boot.Transport
{
    // close失敗を接続単位で上位の停止処理へ返す
    // Return close failures by connection to the owning shutdown operation
    public sealed class WebSocketCloseResult
    {
        public readonly IReadOnlyDictionary<Guid, string> CloseErrors;
        public readonly IReadOnlyDictionary<Guid, string> UnterminatedConnections;
        public bool Succeeded => UnterminatedConnections.Count == 0;

        internal WebSocketCloseResult(Dictionary<Guid, string> closeErrors, Dictionary<Guid, string> unterminatedConnections)
        {
            CloseErrors = new System.Collections.ObjectModel.ReadOnlyDictionary<Guid, string>(closeErrors);
            UnterminatedConnections = new System.Collections.ObjectModel.ReadOnlyDictionary<Guid, string>(unterminatedConnections);
        }
    }
}
