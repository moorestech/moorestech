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
        private static bool _shutdownSubscribed;

        public static WebSocketHub Hub => _hub;

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

        // Editor 経路からの同期クリーンアップ: Kestrel/WS の停止完了を待ってから帰る
        // Synchronous cleanup for editor hooks: waits for Kestrel/WS stop to complete before returning
        private static void CleanupAllSync()
        {
            StopSync();
            // セーフティネット: 何らかの理由で _vite が null でも Vite が残っている可能性に対処
            // Safety net: kill any lingering Vite process even if _vite was null
            ViteProcess.KillAnyLingering();
        }
#endif

        public static async UniTask StartAsync()
        {
            // 二重起動防止
            // Prevent double-start
            if (_kestrel != null) return;

            // GameShutdownEvent 購読はドメイン生存期間に 1 度だけ張る。Stop は idempotent なので複数回起動されても安全
            // Install the GameShutdownEvent subscription exactly once per domain lifetime. Stop is idempotent across cycles
            if (!_shutdownSubscribed)
            {
                GameShutdownEvent.OnGameShutdown.Subscribe(_ => Stop());
                _shutdownSubscribed = true;
            }

            _hub = new WebSocketHub();

            _kestrel = new KestrelServer();
            await _kestrel.StartAsync(_hub);

            _vite = new ViteProcess();
            await _vite.StartAsync();

            Debug.Log("[WebUiHost] ready. Open http://localhost:5173/");
        }

        // 通常経路（GameShutdownEvent 経由）の停止。Kestrel/WS 停止はバックグラウンドに逃がす
        // Normal path (via GameShutdownEvent). Kestrel/WS stop runs on a background thread.
        public static void Stop()
        {
            if (_kestrel == null && _vite == null && _hub == null) return;

            // 先に Vite を kill（同期）。このあと _vite を null クリア。
            // Kill Vite first (synchronous). Only then null the field.
            var vite = _vite;
            vite?.Kill();
            _vite = null;

            // hub / kestrel をスナップショット → フィールドクリア → バックグラウンド停止
            // Snapshot hub/kestrel, clear fields, run stop in background
            var hub = _hub;
            var kestrel = _kestrel;
            _hub = null;
            _kestrel = null;

            // Kestrel・WS 停止はバックグラウンドスレッドで実行（メインスレッドをブロックしない）
            // Stop Kestrel/WS on a background thread (fire-and-forget; don't block Unity main thread)
            if (hub != null || kestrel != null)
            {
                System.Threading.Tasks.Task.Run(async () =>
                {
                    if (hub != null)
                    {
                        hub.ClearTopics();
                        await hub.CloseAllAsync();
                    }
                    if (kestrel != null) await kestrel.StopAsync();
                    Debug.Log("[WebUiHost] stopped");
                });
            }
        }

#if UNITY_EDITOR
        // Editor 経路用の同期停止。Domain Reload 前にポート解放を確実にする
        // Editor-only synchronous stop: guarantees the port is released before Domain Reload
        private static void StopSync()
        {
            if (_kestrel == null && _vite == null && _hub == null) return;

            var vite = _vite;
            vite?.Kill();
            _vite = null;

            var hub = _hub;
            var kestrel = _kestrel;
            _hub = null;
            _kestrel = null;

            if (hub == null && kestrel == null) return;

            // 停止タスクを起動し、WhenAny + Delay で timeout/fault を受け止める（Wait だと再スロー）
            // Run stop task and guard with WhenAny+Delay so timeout/fault don't re-throw (Wait would)
            var stopTask = System.Threading.Tasks.Task.Run(async () =>
            {
                if (hub != null)
                {
                    hub.ClearTopics();
                    await hub.CloseAllAsync();
                }
                if (kestrel != null) await kestrel.StopAsync();
            });
            var timeoutTask = System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(4));
            System.Threading.Tasks.Task.WhenAny(stopTask, timeoutTask).GetAwaiter().GetResult();

            // 例外を「観測済み」にして unobservedTaskException を抑制
            // Observe any fault so it does not surface as UnobservedTaskException
            if (stopTask.IsFaulted)
            {
                Debug.LogWarning($"[WebUiHost] stop faulted: {stopTask.Exception?.GetBaseException().Message}");
            }
            else
            {
                Debug.Log("[WebUiHost] stopped (sync)");
            }
        }
#endif
    }
}
