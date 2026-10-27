using System;
using Client.WebUiHost.Boot;
using UnityEngine;

namespace Client.WebUiHost.Cef
{
    // CEFへ渡したナビゲーション値と実接続を対応付ける
    // Bind the navigation value passed to CEF to its actual connection
    public static class WebUiCefConnection
    {
        private static readonly object Gate = new();
        private static WebSocketHub _hub;
        private static string _navigationId;
        private static Guid _connectionId;

        internal static string BeginNavigation(WebSocketHub hub)
        {
            lock (Gate)
            {
                _hub = hub;
                _navigationId = Guid.NewGuid().ToString("N");
                _connectionId = Guid.Empty;
                return _navigationId;
            }
        }

        internal static bool TryRegister(WebSocketHub hub, string navigationId, Guid connectionId)
        {
            lock (Gate)
            {
                if (!ReferenceEquals(hub, _hub) || navigationId != _navigationId)
                {
                    Debug.LogWarning("[WebUiHost] CEF connection registration rejected: stale navigation");
                    return false;
                }
                // 同じナビゲーションの再接続（ページの再読込）は最新の接続で上書きする
                // A reconnect under the same navigation (a page reload) overwrites with the latest connection
                if (_connectionId != Guid.Empty) Debug.LogWarning($"[WebUiHost] CEF connection replaced under the same navigation: {_connectionId} -> {connectionId}");
                _connectionId = connectionId;
                return true;
            }
        }

        internal static void Release(WebSocketHub hub, Guid connectionId)
        {
            lock (Gate)
            {
                // 古い接続の終了で新しい接続を消さない
                // Do not let an old disconnect erase a newer connection
                if (ReferenceEquals(hub, _hub) && _connectionId == connectionId) _connectionId = Guid.Empty;
            }
        }

        public static bool TryGetConnection(WebSocketHub hub, out Guid connectionId)
        {
            lock (Gate)
            {
                connectionId = _connectionId;
                return ReferenceEquals(hub, _hub) && connectionId != Guid.Empty && hub.HasConnection(connectionId);
            }
        }
    }
}
