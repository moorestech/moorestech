using System;
using System.Threading.Tasks;
using Client.Game.Common;
using Client.WebUiHost.Vite;
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
        private static ViteProcess _vite;
        private static WebSocketHub _hub;
        private static IDisposable _shutdownSubscription;

        // 進行中の停止タスク。StartAsync がこれを await してポート解放を待つ
        // Tracks the in-flight stop task; StartAsync awaits it to ensure the port is released
        private static Task _stopTask = Task.CompletedTask;

        public static WebSocketHub Hub => _hub;

        public static async UniTask<bool> StartAsync()
        {
            // 前回の停止完了を待つ（連続 Start/Stop で port 5050 衝突を避ける）
            // Wait for the previous stop to complete (avoids port 5050 collision on rapid restart)
            if (!_stopTask.IsCompleted)
            {
                // 前回停止の fault は StopAsync 内でログ済み。ここでは待つだけで再 throw させない（2-B）
                // The previous stop's fault is already logged inside StopAsync; only wait here, do not rethrow (2-B)
                await _stopTask.ContinueWith(_ => { });
            }
            // 起動済みならtrueを返す
            // Return true if already running
            if (_kestrel != null) return true;

            // GameShutdownEvent 購読はドメイン寿命で 1 度だけ張る。IDisposable で保持し意図を型で表現
            // Install the GameShutdownEvent subscription exactly once per domain; hold it as IDisposable
            _shutdownSubscription ??= GameShutdownEvent.OnGameShutdown.Subscribe(_ => Stop());

            // ローカルで起動し、成功後にフィールドへ代入する。途中失敗時は起動済みを片付け null 残留させない（2-A）
            // Start into locals and assign fields only on success; on partial failure, tear down and leave fields null (2-A)
            var hub = new WebSocketHub();
            var kestrel = new KestrelServer();
            ViteProcess vite = null;
            var kestrelStarted = false;
            var started = false;
            try
            {
                await kestrel.StartAsync(hub);
                kestrelStarted = true;

                vite = new ViteProcess();

                // Vite が起動できない（node 欠如・ready 未達）と無 UI になるため、失敗扱いでロールバックする
                // A Vite failure (missing node / not ready) leaves the UI blank, so roll back as a failure
                if (!await vite.StartAsync()) return false;

                _hub = hub;
                _kestrel = kestrel;
                _vite = vite;
                started = true;
            }
            finally
            {
                // 起動途中で失敗したら、握ったポート/プロセスを解放してフィールドを null のまま残す
                // If startup failed midway, release the held port/process and leave the fields null
                //
                // 後始末の例外が元の起動例外をマスクしないよう各ステップを隔離してログのみに留める（2-B）
                // Isolate each cleanup step and only log its fault so it cannot mask the original startup exception (2-B)
                if (!started)
                {
                    try { vite?.Kill(); }
                    catch (Exception e) { Debug.LogWarning($"[WebUiHost] rollback vite kill failed: {e.GetBaseException().Message}"); }

                    if (kestrelStarted)
                    {
                        try { await kestrel.StopAsync(); }
                        catch (Exception e) { Debug.LogWarning($"[WebUiHost] rollback kestrel stop failed: {e.GetBaseException().Message}"); }
                    }
                }
            }

            Debug.Log("[WebUiHost] ready. Open http://localhost:5173/");
            return true;
        }

        // 停止開始（通常経路）。停止タスクをフィールドに保持し Forget で投げる
        // Begin stopping (normal path). The stop task is stored as a field and fired with Forget
        public static void Stop()
        {
            _stopTask = StopAsync().AsTask();
            _stopTask.AsUniTask().Forget(e => Debug.LogWarning($"[WebUiHost] stop faulted: {e.GetBaseException().Message}"));
        }

        // 実際の停止シーケンス。通常経路と Editor hook 経路で共有する
        // Actual stop sequence shared by normal path and editor hook path
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

            // 各停止ステップを個別に隔離してログし、1 つが失敗しても全ステップを完走させる（2-B）
            // Isolate and log each stop step so one failure cannot skip the remaining steps (2-B)
            if (vite != null)
            {
                // Vite kill はメインスレッドで同期実行（Process.Kill はメインスレッド前提）
                // Kill Vite synchronously on the main thread (Process.Kill assumes main thread)
                try { vite.Kill(); }
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

#if UNITY_EDITOR
        // Play mode 終了・ドメインリロード・エディタ終了のいずれでも確実にクリーンアップ
        // Clean up on Play mode exit, domain reload, or editor quit — whichever fires first
        //   - ExitingPlayMode: Reload Domain = off の場合に beforeAssemblyReload が来ない穴を埋める
        //   - beforeAssemblyReload: Reload Domain = on の通常経路
        //   - quitting: Play mode に入らずにエディタを閉じた場合
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterDomainReloadHook()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += CleanupAllSync;
            UnityEditor.EditorApplication.quitting += CleanupAllSync;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            // Play mode 終了直前にクリーンアップ。Domain Reload が走る前にポート解放を完了させる
            // Clean up just before exiting play mode so ports are released before Domain Reload
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                CleanupAllSync();
            }
        }

        // Editor hook は同期完了を要求するので、Stop 経路と同じ StopAsync を最大 4 秒待つ
        // Editor hooks need synchronous completion; wait up to 4s on the shared StopAsync
        private static void CleanupAllSync()
        {
            Stop();
            Task.WhenAny(_stopTask, Task.Delay(TimeSpan.FromSeconds(4))).GetAwaiter().GetResult();
            if (_stopTask.IsFaulted)
            {
                Debug.LogWarning($"[WebUiHost] stop faulted: {_stopTask.Exception?.GetBaseException().Message}");
            }
            // セーフティネット: Vite が残っていたら port ベースで特定して kill
            // Safety net: if any Vite is still holding the port, resolve its pid and kill
            ViteProcessKiller.KillAnyLingering();
        }
#endif
    }
}
