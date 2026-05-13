using System;
using System.Threading.Tasks;
using Client.Game.Common;
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

        public static async UniTask StartAsync()
        {
            // 前回の停止完了を待つ（連続 Start/Stop で port 5050 衝突を避ける）
            // Wait for the previous stop to complete (avoids port 5050 collision on rapid restart)
            if (!_stopTask.IsCompleted)
            {
                await _stopTask;
            }
            if (_kestrel != null) return;

            // GameShutdownEvent 購読はドメイン寿命で 1 度だけ張る。IDisposable で保持し意図を型で表現
            // Install the GameShutdownEvent subscription exactly once per domain; hold it as IDisposable
            _shutdownSubscription ??= GameShutdownEvent.OnGameShutdown.Subscribe(_ => Stop());

            _hub = new WebSocketHub();
            _kestrel = new KestrelServer();
            await _kestrel.StartAsync(_hub);

            _vite = new ViteProcess();
            await _vite.StartAsync();

            Debug.Log("[WebUiHost] ready. Open http://localhost:5173/");
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
            // Vite はメインスレッドで同期 kill（Process.Kill はメインスレッド前提）
            // Kill Vite synchronously on the main thread (Process.Kill assumes main thread)
            if (_vite != null)
            {
                _vite.Kill();
                _vite = null;
            }

            // Kestrel/WS 停止はスレッドプールへ逃がしてメインスレッドを解放
            // Move Kestrel/WS shutdown off the main thread via UniTask.SwitchToTaskPool
            await UniTask.SwitchToTaskPool();

            if (_hub != null)
            {
                _hub.ClearTopics();
                await _hub.CloseAllAsync();
                _hub = null;
            }

            if (_kestrel != null)
            {
                await _kestrel.StopAsync();
                _kestrel = null;
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
            ViteProcess.KillAnyLingering();
        }
#endif
    }
}
