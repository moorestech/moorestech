using System;
using System.Threading;
using System.Threading.Tasks;
using Client.Game.Common;
using Client.WebUiHost.Common;
using Client.WebUiHost.Boot.Transport;
using Client.WebUiHost.Vite;
using Client.WebUiHost.Static;
using Cysharp.Threading.Tasks;
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
        private static readonly WebUiHostShutdownParticipant ShutdownParticipant = new();

        private static Task<WebUiStopResult> _stopTask;
        public static WebSocketHub Hub => _hub;


        public static string WebUiUrl => _webUiUrl;
        private static string _webUiUrl;
        // exitToken はPlay終了で発火し、Kestrel起動やVite疎通のTask継続がEditModeで再開するのを止める
        // exitToken fires on play-mode exit and stops Kestrel startup / Vite probe continuations from resuming in EditMode
        public static async UniTask<bool> StartAsync(CancellationToken exitToken)
        {
            // 前回の停止完了を待つ（連続 Start/Stop での自ポート衝突を避ける）
            // Wait for the previous stop to complete (avoids colliding with our own ports on rapid restart)
            if (_stopTask != null && !_stopTask.IsCompleted)
            {
                // 前回停止の fault は StopAsync 内でログ済み。ここでは待つだけで再 throw させない（2-B）
                // The previous stop's fault is already logged inside StopAsync; only wait here, do not rethrow (2-B)
                await _stopTask.ContinueWith(_ => { });
            }
            GameShutdownEvent.RegisterParticipant(ShutdownParticipant);
            if (_kestrel != null) return true;
            // 新しいHost起動を開始するときだけ停止結果の世代を更新する
            // Reset the retained stop result only when starting a new host lifetime
            _stopTask = null;

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
                        Debug.LogError($"[WebUiHost] production artifact rejected: {failure}; Web UI remains unavailable");
                        return false;
                    }
                    staticFiles = new WebUiStaticFileEndpoint(WebUiPaths.ProductionDistRoot);
                }

                await kestrel.StartAsync(hub, staticFiles, exitToken);
                kestrelStarted = true;

                if (mode == WebUiHostMode.Development)
                {
                    vite = new ViteSupervisor();
                    // HTTP疎通後に起動成功とする
                    // A Vite instance without successful HTTP health leaves no UI, so roll it back as startup failure
                    if (!await vite.StartAsync(kestrel.ActualPort, exitToken)) return false;
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
            StopAsync().Forget(e => Debug.LogWarning($"[WebUiHost] stop faulted: {e.GetBaseException().Message}"));
        }

        public static UniTask<WebUiStopResult> StopAsync()
        {
            if (_stopTask == null) _stopTask = StopCoreAsync().AsTask();
            return _stopTask.AsUniTask();
        }

        private static async UniTask<WebUiStopResult> StopCoreAsync()
        {
            // フィールドをローカルへ退避し即 null 化する。二重 Stop を冪等にし、以後の Start を再試行可能にする
            // Move fields to locals and null them immediately; makes double-stop idempotent and lets a later Start retry
            var vite = _vite;
            var hub = _hub;
            var kestrel = _kestrel;
            _vite = null;
            _hub = null;
            _kestrel = null;

            // 実ポート公開を取り下げる（停止中の CORS 全拒否・CEF ナビゲーション抑止）
            // Withdraw the published port (rejects CORS and suppresses CEF navigation while stopped)
            _webUiUrl = null;
            WebUiPortConfig.SetBrowserPort(0);

            return await WebUiHostShutdown.StopAsync(vite, hub, kestrel);
        }

        // Editor フックから呼ぶ同期停止。停止タスクの完了を最大 timeout まで待つ
        // Synchronous stop for editor hooks; waits up to timeout for the stop task
        public static void StopAndWaitSync(TimeSpan timeout)
        {
            Stop();
            Task.WhenAny(_stopTask, Task.Delay(timeout)).GetAwaiter().GetResult();
            if (_stopTask.Status == TaskStatus.RanToCompletion && !_stopTask.Result.Succeeded)
                Debug.LogError("[WebUiHost] editor cleanup completed with shutdown failures");
            if (_stopTask.IsFaulted)
            {
                Debug.LogWarning($"[WebUiHost] stop faulted: {_stopTask.Exception?.GetBaseException().Message}");
            }
        }
    }
}
