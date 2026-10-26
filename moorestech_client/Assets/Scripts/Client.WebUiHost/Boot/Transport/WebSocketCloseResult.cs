using System;
using System.Collections.Generic;

namespace Client.WebUiHost.Boot.Transport
{
    // close失敗を接続単位で上位の停止処理へ返す
    // Return close failures by connection to the owning shutdown operation
    public sealed class WebSocketCloseResult
    {
        public readonly IReadOnlyDictionary<Guid, string> FailedConnections;
        public bool Succeeded => FailedConnections.Count == 0;

        internal WebSocketCloseResult(Dictionary<Guid, string> failedConnections)
        {
            FailedConnections = new System.Collections.ObjectModel.ReadOnlyDictionary<Guid, string>(failedConnections);
        }
    }
}
