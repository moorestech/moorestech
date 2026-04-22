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

            // 先に WS を閉じてから HTTP を止め、最後に Vite を kill
            // Close WS first, stop HTTP, then kill Vite
            _hub?.CloseAllAsync().GetAwaiter().GetResult();
            _kestrel.StopAsync().GetAwaiter().GetResult();
            _vite?.Kill();

            _hub = null;
            _kestrel = null;
            _vite = null;
        }
    }
}
