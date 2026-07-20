using System;
using System.Threading.Tasks;
using Client.Game.Common;
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Common;
using Client.WebUiHost.Vite;
using Client.WebUiHost.Static;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// Web UI ホストの起動/停止の静的 facade。
    /// Web UI host static facade for start/stop and Hub access.
    /// </summary>
    public static class WebUiHost
    {
        private static KestrelServer _kestrel;
        private static ViteSupervisor _vite;
        private static WebSocketHub _hub;
        private static IDisposable _shutdownSubscription;
        private static IDisposable _viteAvailabilitySubscription;

        private static Task _stopTask = Task.CompletedTask;
        public static WebSocketHub Hub => _hub;

        public static string WebUiUrl => _webUiUrl;
        private static string _webUiUrl;
        public static async UniTask<bool> StartAsync()
        {
            // 前回の停止完了を待つ（連続 Start/Stop での自ポート衝突を避ける）
            // Wait for the previous stop to complete (avoids colliding with our own ports on rapid restart)
            if (!_stopTask.IsCompleted)
            {
                // 前回停止の fault は StopAsync 内でログ済み。ここでは待つだけで再 throw させない（2-B）
                // The previous stop's fault is already logged inside StopAsync; only wait here, do not rethrow (2-B)
                await _stopTask.ContinueWith(_ => { });
            }
            if (_kestrel != null) return true;

            _shutdownSubscription ??= GameShutdownEvent.OnGameShutdown.Subscribe(_ => Stop());

            // ローカルで起動し、成功後にフィールドへ代入する。途中失敗時は起動済みを片付け null 残留させない（2-A）
            // Start into locals and assign fields only on success; on partial failure, tear down and leave fields null (2-A)
            var hub = new WebSocketHub();
            var kestrel = new KestrelServer();
            ViteSupervisor vite = null;
            WebUiStaticFileEndpoint staticFiles = null;
            var kestrelStarted = false;
            var started = false;
            try
            {
                var mode = WebUiHostModeResolver.Resolve();
                if (mode == WebUiHostMode.Production)
                {
                    // 配信開始前に成果物を検証する
                    // Validate the entire artifact before opening Kestrel so web mode never becomes available on failure
                    if (!WebUiArtifactValidator.TryValidate(WebUiPaths.ProductionDistRoot, out var failure))
                    {
                        Debug.LogError($"[WebUiHost] production artifact rejected: {failure}; falling back to uGUI");
                        return false;
                    }
                    staticFiles = new WebUiStaticFileEndpoint(WebUiPaths.ProductionDistRoot);
                }

                await kestrel.StartAsync(hub, staticFiles);
                kestrelStarted = true;

                if (mode == WebUiHostMode.Development)
                {
                    vite = new ViteSupervisor();
                    // HTTP疎通後に起動成功とする
                    // A Vite instance without successful HTTP health leaves no UI, so roll it back as startup failure
                    if (!await vite.StartAsync(kestrel.ActualPort)) return false;
                    _viteAvailabilitySubscription = vite.Availability.Subscribe(WebUiScreenGate.SetHostAvailable);
                    WebUiPortConfig.SetBrowserPort(vite.ActualPort);
                    _webUiUrl = $"http://127.0.0.1:{vite.ActualPort}/";
                }
                else
                {
                    WebUiPortConfig.SetBrowserPort(kestrel.ActualPort);
                    _webUiUrl = $"http://127.0.0.1:{kestrel.ActualPort}/";
                }

                _hub = hub;
                _kestrel = kestrel;
                _vite = vite;
                started = true;
            }
            finally
            {
                // 起動途中で失敗したら、握ったポート/プロセスを解放してフィールドを null のまま残す
                // If startup failed midway, release the held port/process and leave the fields null
                if (!started)
                {
                    // 後始末例外で起動例外を隠さない
                    // Isolate cleanup failures so they cannot hide the startup failure
                    try { vite?.Stop(); }
                    catch (Exception e) { Debug.LogWarning($"[WebUiHost] rollback vite kill failed: {e.GetBaseException().Message}"); }

                    if (kestrelStarted)
                    {
                        try { await kestrel.StopAsync(); }
                        catch (Exception e) { Debug.LogWarning($"[WebUiHost] rollback kestrel stop failed: {e.GetBaseException().Message}"); }
                    }
                }
            }
            Debug.Log($"[WebUiHost] ready. Open {WebUiUrl}");
            return true;
        }
        // 停止開始（通常経路）。停止タスクをフィールドに保持し Forget で投げる
        // Begin stopping (normal path). The stop task is stored as a field and fired with Forget
        public static void Stop()
        {
            _stopTask = StopAsync().AsTask();
            _stopTask.AsUniTask().Forget(e => Debug.LogWarning($"[WebUiHost] stop faulted: {e.GetBaseException().Message}"));
        }

        public static async UniTask StopAsync()
        {
            // フィールドをローカルへ退避し即 null 化する。二重 Stop を冪等にし、以後の Start を再試行可能にする
            // Move fields to locals and null them immediately; makes double-stop idempotent and lets a later Start retry
            var vite = _vite;
            var hub = _hub;
            var kestrel = _kestrel;
            _vite = null;
            _hub = null;
            _kestrel = null;
            _viteAvailabilitySubscription?.Dispose();
            _viteAvailabilitySubscription = null;
            WebUiScreenGate.SetHostAvailable(false);

            // 実ポート公開を取り下げる（停止中の CORS 全拒否・CEF ナビゲーション抑止）
            // Withdraw the published port (rejects CORS and suppresses CEF navigation while stopped)
            _webUiUrl = null;
            WebUiPortConfig.SetBrowserPort(0);

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
                Game.ItemIconEndpoint.ClearCache();
                Game.BlockIconEndpoint.ClearCache();
                Game.TrainCarIconEndpoint.ClearCache();
            }
            catch (Exception e) { Debug.LogWarning($"[WebUiHost] icon cache clear failed: {e.GetBaseException().Message}"); }

            if (hub != null)
            {
                // トピック/アクションのバインド解除
                // Release topic/action bindings
                try { hub.ClearBindings(); }
                catch (Exception e) { Debug.LogWarning($"[WebUiHost] clear bindings failed: {e.GetBaseException().Message}"); }

                // 全 WS 接続を close（送信ループと競合しない CloseAsync 経由）
                // Close all WS connections (via CloseAsync, which does not race the send loop)
                try { await hub.CloseAllAsync(); }
                catch (Exception e) { Debug.LogWarning($"[WebUiHost] close all failed: {e.GetBaseException().Message}"); }
            }

            if (kestrel != null)
            {
                // Kestrel 停止でポートを確実に解放する（次回 Start の port 5050 衝突を防ぐ）
                // Stop Kestrel to release the port for sure (prevents port 5050 collision on next Start)
                try { await kestrel.StopAsync(); }
                catch (Exception e) { Debug.LogWarning($"[WebUiHost] kestrel stop failed: {e.GetBaseException().Message}"); }
            }

            Debug.Log("[WebUiHost] stopped");
        }

        // Editor フックから呼ぶ同期停止。停止タスクの完了を最大 timeout まで待つ
        // Synchronous stop for editor hooks; waits up to timeout for the stop task
        public static void StopAndWaitSync(TimeSpan timeout)
        {
            Stop();
            Task.WhenAny(_stopTask, Task.Delay(timeout)).GetAwaiter().GetResult();
            if (_stopTask.IsFaulted)
            {
                Debug.LogWarning($"[WebUiHost] stop faulted: {_stopTask.Exception?.GetBaseException().Message}");
            }
        }
    }
}
