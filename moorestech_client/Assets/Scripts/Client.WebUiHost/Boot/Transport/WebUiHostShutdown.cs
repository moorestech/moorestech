using System;
using Client.WebUiHost.Vite;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.WebUiHost.Boot.Transport
{
    internal static class WebUiHostShutdown
    {
        internal static async UniTask<WebSocketCloseResult> StopAsync(ViteSupervisor vite, WebSocketHub hub, KestrelServer kestrel)
        {
            var closeResult = new WebSocketCloseResult(new(), new());
            // 各停止ステップを個別に隔離してログし、1 つが失敗しても全ステップを完走させる（2-B）
            // Isolate and log each stop step so one failure cannot skip the remaining steps (2-B)
            if (vite != null)
            {
                // Vite kill はメインスレッドで同期実行（Process.Kill はメインスレッド前提）
                // Kill Vite synchronously on the main thread (Process.Kill assumes main thread)
                try { vite.Stop(); }
                catch (Exception e) { Debug.LogWarning($"[WebUiHost] vite kill failed: {e.GetBaseException().Message}"); }
            }

            // Kestrel/WS 停止はスレッドプールへ逃がしてメインスレッドを解放
            // Move Kestrel/WS shutdown off the main thread via UniTask.SwitchToTaskPool
            await UniTask.SwitchToTaskPool();

            // セーブデータ/Mod 切替に備えてアイコンキャッシュを破棄する
            // Drop icon caches in case the save data / mod set changes
            try
            {
                Game.Icons.IconEndpoint.ClearAllCaches();
            }
            catch (Exception e) { Debug.LogWarning($"[WebUiHost] icon cache clear failed: {e.GetBaseException().Message}"); }

            if (hub != null)
            {
                // トピック/アクションのバインド解除
                // Release topic/action bindings
                try { hub.ClearBindings(); }
                catch (Exception e) { Debug.LogWarning($"[WebUiHost] clear bindings failed: {e.GetBaseException().Message}"); }

                // WebSocketネットワーク送信の失敗結果を保持し、Kestrel停止まで続ける
                // Retain WebSocket network transmission failures and continue through Kestrel shutdown
                closeResult = await hub.CloseAllAsync();
            }

            if (kestrel != null)
            {
                // Kestrel 停止でポートを確実に解放する（次回 Start の port 5050 衝突を防ぐ）
                // Stop Kestrel to release the port for sure (prevents port 5050 collision on next Start)
                try { await kestrel.StopAsync(); }
                catch (Exception e) { Debug.LogWarning($"[WebUiHost] kestrel stop failed: {e.GetBaseException().Message}"); }
            }

            if (closeResult.Succeeded) Debug.Log("[WebUiHost] stopped");
            else Debug.LogError($"[WebUiHost] stopped with unterminated WebSockets: {string.Join(", ", closeResult.UnterminatedConnections)}");
            return closeResult;
        }
    }
}
