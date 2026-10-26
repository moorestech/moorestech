using System;
using System.Collections.Generic;
using Client.WebUiHost.Vite;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.WebUiHost.Boot.Transport
{
    internal static class WebUiHostShutdown
    {
        internal static async UniTask<WebUiStopResult> StopAsync(ViteSupervisor vite, WebSocketHub hub, KestrelServer kestrel)
        {
            var resourceFailures = new Dictionary<string, string>();
            var closeResult = new WebSocketCloseResult(new(), new());
            // 各停止ステップを個別に隔離してログし、1 つが失敗しても全ステップを完走させる（2-B）
            // Isolate and log each stop step so one failure cannot skip the remaining steps (2-B)
            if (vite != null)
            {
                // ViteのOSプロセス終了境界を隔離し、欠損を結果へ残す
                // Isolate the Vite OS process termination boundary and retain the failure in the result
                try { vite.Stop(); }
                catch (Exception e) { RecordFailure("Vite process", e, resourceFailures); }
            }

            // Kestrel/WS 停止はスレッドプールへ逃がしてメインスレッドを解放
            // Move Kestrel/WS shutdown off the main thread via UniTask.SwitchToTaskPool
            await UniTask.SwitchToTaskPool();

            if (hub != null)
            {
                // WebSocketネットワーク送信の失敗結果を保持し、Kestrel停止まで続ける
                // Retain WebSocket network transmission failures and continue through Kestrel shutdown
                closeResult = await hub.CloseAllAsync();
            }

            if (kestrel != null)
            {
                // Kestrelのネットワークリスナー停止境界を隔離して欠損を返す
                // Isolate Kestrel network listener shutdown and return its failure
                try { await kestrel.StopAsync(); }
                catch (Exception e) { RecordFailure("Kestrel listener", e, resourceFailures); }
            }

            // 内部状態の解除は外部資源停止後に行い、内部障害でネットワーク停止を飛ばさない
            // Clear internal state after external resources so internal faults cannot skip network shutdown
            Game.Icons.IconEndpoint.ClearAllCaches();
            hub?.ClearBindings();
            var result = new WebUiStopResult(closeResult, resourceFailures);
            if (result.Succeeded) Debug.Log("[WebUiHost] stopped");
            else Debug.LogError($"[WebUiHost] shutdown incomplete; sockets: {string.Join(", ", closeResult.UnterminatedConnections)}; resources: {string.Join(", ", resourceFailures)}");
            return result;
        }

        private static void RecordFailure(string resource, Exception error, Dictionary<string, string> failures)
        {
            failures.Add(resource, error.GetBaseException().Message);
            Debug.LogError($"[WebUiHost] {resource} stop failed: {failures[resource]}");
        }
    }
}
