using System;
using System.Collections.Generic;

namespace Client.WebUiHost.Boot.Transport
{
    // 終了しきれなかった接続を上位の停止処理へ返す（個々の close 失敗はログ済み）
    // Return connections left unterminated to the owning shutdown (each close failure is already logged)
    public sealed class WebSocketCloseResult
    {
        public readonly IReadOnlyDictionary<Guid, string> UnterminatedConnections;
        public bool Succeeded => UnterminatedConnections.Count == 0;

        internal WebSocketCloseResult(Dictionary<Guid, string> unterminatedConnections)
        {
            UnterminatedConnections = new System.Collections.ObjectModel.ReadOnlyDictionary<Guid, string>(unterminatedConnections);
        }
    }
}
