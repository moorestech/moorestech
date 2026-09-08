using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.WebUiHost.Vite
{
    /// <summary>
    /// Viteの起動・死活監視・同一ポート再起動を管理する
    /// Manages Vite startup, health monitoring, and same-port restarts
    /// </summary>
    public class ViteSupervisor
    {
        private const int FailureThreshold = 3;
        private const int ProbeIntervalMilliseconds = 3000;
        private const int RestartDelayMilliseconds = 2000;
        private readonly CancellationTokenSource _stopping = new();
        private ViteProcess _process;
        private int _kestrelPort;
        private int _vitePort;

        public int ActualPort => _vitePort;

        public async UniTask<bool> StartAsync(int kestrelPort)
        {
            _kestrelPort = kestrelPort;
            _process = new ViteProcess();
            if (!await _process.StartAsync(kestrelPort, Common.WebUiPortConfig.ViteBasePort)) return false;
            _vitePort = _process.ActualPort;

            // HTTP応答後に起動成功とする
            // Require an HTTP response in addition to stdout ready before declaring startup success
            if (!await ViteHealthProbe.IsHealthyAsync(_vitePort, TimeSpan.FromSeconds(3)))
            {
                Debug.LogError($"[WebUiHost] Vite health check failed during startup on port {_vitePort}");
                _process.Kill();
                return false;
            }

            MonitorAsync().Forget();
            return true;
        }

        public void Stop()
        {
            if (!_stopping.IsCancellationRequested) _stopping.Cancel();
            _process?.Kill();
            _process = null;
        }

        private async UniTaskVoid MonitorAsync()
        {
            var consecutiveFailures = 0;
            while (!_stopping.IsCancellationRequested)
            {
                await UniTask.Delay(ProbeIntervalMilliseconds, cancellationToken: _stopping.Token).SuppressCancellationThrow();
                if (_stopping.IsCancellationRequested) return;

                var healthy = await ViteHealthProbe.IsHealthyAsync(_vitePort, TimeSpan.FromSeconds(2));
                consecutiveFailures = healthy ? 0 : consecutiveFailures + 1;
                if (consecutiveFailures < FailureThreshold) continue;

                // URLを保つため同一ポートで再起動する（uGUIフォールバックは撤去済み: ADR 0052）
                // Restart on the same port to preserve the URL; the uGUI fallback is gone (ADR 0052)
                Debug.LogError($"[WebUiHost] Vite unhealthy on port {_vitePort}; restarting on the same port");
                _process?.Kill();
                _process = null;
                consecutiveFailures = 0;

                while (!_stopping.IsCancellationRequested && !await TryRestartAsync())
                {
                    Debug.LogWarning($"[WebUiHost] Vite restart failed on port {_vitePort}; retrying");
                    await UniTask.Delay(RestartDelayMilliseconds, cancellationToken: _stopping.Token).SuppressCancellationThrow();
                }
            }
        }

        private async UniTask<bool> TryRestartAsync()
        {
            var replacement = new ViteProcess();
            if (!await replacement.StartAsync(_kestrelPort, _vitePort))
            {
                replacement.Kill();
                return false;
            }
            if (replacement.ActualPort != _vitePort || !await ViteHealthProbe.IsHealthyAsync(_vitePort, TimeSpan.FromSeconds(3)))
            {
                replacement.Kill();
                return false;
            }
            if (_stopping.IsCancellationRequested)
            {
                replacement.Kill();
                return false;
            }

            _process = replacement;
            Debug.Log($"[WebUiHost] Vite recovered on port {_vitePort}");
            return true;
        }
    }
}
