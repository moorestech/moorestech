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
        private static ViteProcess _vite;
        private static WebSocketHub _hub;

        public static WebSocketHub Hub => _hub;

#if UNITY_EDITOR
        // Play mode 終了・ドメインリロード・エディタ終了のいずれでも確実にクリーンアップ
        // Clean up on Play mode exit, domain reload, or editor quit — whichever fires first
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterDomainReloadHook()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += CleanupAll;
            UnityEditor.EditorApplication.quitting += CleanupAll;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            // Play mode 終了直前に Vite を止める（BackToMainMenu.OnDestroy より確実）
            // Stop Vite just before exiting play mode (more reliable than BackToMainMenu.OnDestroy)
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                CleanupAll();
            }
        }

        private static void CleanupAll()
        {
            Stop();
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

            _hub = new WebSocketHub();

            _kestrel = new KestrelServer();
            await _kestrel.StartAsync(_hub);

            _vite = new ViteProcess();
            await _vite.StartAsync();

            Debug.Log("[WebUiHost] ready. Open http://localhost:5173/");
        }

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
                    if (hub != null) await hub.CloseAllAsync();
                    if (kestrel != null) await kestrel.StopAsync();
                    Debug.Log("[WebUiHost] stopped");
                });
            }
        }
    }
}
