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
        // ドメインリロード前に Kestrel + Vite + WS を停止してリソースリークを防ぐ
        // Stop Kestrel + Vite + WS before domain reload to prevent resource leaks
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterDomainReloadHook()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                // スナップショット → 先にフィールドクリア
                // Snapshot refs first, clear fields
                var hub = _hub;
                var kestrel = _kestrel;
                var vite = _vite;
                _hub = null;
                _kestrel = null;
                _vite = null;

                // Vite を即 kill
                // Kill Vite immediately
                vite?.Kill();

                // Kestrel と WS の停止を待つ（最大 4 秒）
                // Wait for Kestrel/WS to stop (cap 4 seconds total)
                // CloseAsync / StopAsync それぞれ 2 秒タイムアウト付きなのでブロックしても安全
                // Both CloseAsync and StopAsync have 2-second timeouts, so blocking is safe
                if (hub != null || kestrel != null)
                {
                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        if (hub != null) await hub.CloseAllAsync();
                        if (kestrel != null) await kestrel.StopAsync();
                    }).GetAwaiter().GetResult();
                }
            };
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
            if (_kestrel == null) return;

            // スナップショットを取ってから先にフィールドをクリア（再帰防止）
            // Snapshot refs first, clear fields to prevent re-entry
            var hub = _hub;
            var kestrel = _kestrel;
            var vite = _vite;
            _hub = null;
            _kestrel = null;
            _vite = null;

            // Vite を即 kill（非同期不要）
            // Kill Vite immediately (no async needed)
            vite?.Kill();

            // Kestrel・WS 停止はバックグラウンドスレッドで実行（メインスレッドをブロックしない）
            // Stop Kestrel/WS on a background thread (fire-and-forget; don't block Unity main thread)
            System.Threading.Tasks.Task.Run(async () =>
            {
                if (hub != null) await hub.CloseAllAsync();
                if (kestrel != null) await kestrel.StopAsync();
                Debug.Log("[WebUiHost] stopped");
            });
        }
    }
}
