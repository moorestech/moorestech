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
            // 最初の await より前に停止中を確定し、停止待ちの間に action を実行させない
            // Commit the stopping state before the first await so no action runs while stopping
            hub?.SetStopping();
            var resourceFailures = new Dictionary<string, string>();
            var closeResult = new WebSocketCloseResult(new());
            // 各停止ステップを個別に隔離してログし、1 つが失敗しても全ステップを完走させる（2-B）
            // Isolate and log each stop step so one failure cannot skip the remaining steps (2-B)
            if (vite != null)
            {
                // 外部プロセス（Viteと子の終了コマンド）境界を隔離し、欠損を結果へ残す
                // Isolate the external process boundary (Vite and its child kill commands) and keep the failure in the result
                // Process.Kill はメインスレッド前提なので、スレッドプールへ移る前に同期で止める
                // Process.Kill assumes the main thread, so stop it synchronously before moving to the thread pool
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
