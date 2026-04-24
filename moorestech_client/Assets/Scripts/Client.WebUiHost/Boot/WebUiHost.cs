using Client.Common.Shutdown;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.WebUiHost.Boot
{
    // Web UI ホストの起動 facade。停止は ShutdownCoordinator が一元管理する
    // Static facade for Web UI host start; stop is centrally managed by ShutdownCoordinator
    public static class WebUiHost
    {
        private static KestrelServer _kestrel;
        private static ViteProcess _vite;
        private static WebSocketHub _hub;
        private static bool _registered;

        public static WebSocketHub Hub => _hub;

        public static async UniTask StartAsync()
        {
            if (_kestrel != null) return;

            // 初回起動時に 1 度だけ終了パイプラインへ登録
            // Register the stop step into the shutdown pipeline exactly once
            if (!_registered)
            {
                ShutdownCoordinator.Register(ShutdownPhase.AfterDisconnect, "WebUiHost.Stop", StopAsync);
                _registered = true;
            }

            _hub = new WebSocketHub();
            _kestrel = new KestrelServer();
            await _kestrel.StartAsync(_hub);

            _vite = new ViteProcess();
            await _vite.StartAsync();

            Debug.Log("[WebUiHost] ready. Open http://localhost:5173/");
        }

        public static async UniTask StopAsync()
        {
            // Vite はメインスレッドで同期 kill
            // Kill Vite synchronously on the main thread
            if (_vite != null)
            {
                _vite.Kill();
                _vite = null;
            }

            // Kestrel/WS 停止はスレッドプールへ逃がしてメインスレッドを解放
            // Move Kestrel/WS shutdown off the main thread
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

            // 残存 Vite のセーフティネット
            // Safety net in case any lingering Vite process still holds the port
            ViteProcess.KillAnyLingering();

            Debug.Log("[WebUiHost] stopped");
        }
    }
}
