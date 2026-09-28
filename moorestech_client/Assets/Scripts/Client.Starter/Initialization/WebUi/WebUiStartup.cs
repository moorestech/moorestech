using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.Initialization.WebUi
{
    // Web UI のネットワーク待受失敗を境界で隔離する
    // Isolate Web UI network-listener failures at the boundary
    internal static class WebUiStartup
    {
        internal static async UniTask StartAsync(CancellationToken exitToken)
        {
            // ---- Web UI サーバーの起動（最序盤）----
            // GameShutdownEvent の購読は WebUiHost 側で 1 度だけ張られる
            // ---- Web UI server bootstrap (earliest phase) ----
            // The GameShutdownEvent subscription is installed once inside WebUiHost itself
            //
            // 起動失敗でも継続、UIはWeb一本のため非表示
            // Web UI startup failure does not block gameplay, but the screen UI is web-only so nothing is shown
            try
            {
                await Client.WebUiHost.Boot.WebUiHost.StartAsync(exitToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // WebUI 無しでゲーム続行。待受起動失敗をログして再試行可能にする
                // Continue without WebUI; log listener startup failure and allow retry
                Debug.LogWarning($"[WebUiHost] start skipped: {e.Message}");
            }
        }
    }
}
