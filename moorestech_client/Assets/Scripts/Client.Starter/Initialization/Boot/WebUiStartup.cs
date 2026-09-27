using System;
using System.Threading;
using Client.RemoteExec;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.Initialization.Boot
{
    internal static class WebUiStartup
    {
        internal static async UniTask StartAsync(CancellationToken exitToken)
        {
            // 外部プロセスの起動境界を隔離し、ゲームの初期化を続ける
            // Isolate external process startup failures and continue game initialization
            try
            {
                await Client.WebUiHost.Boot.WebUiHost.StartAsync(exitToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Debug.LogWarning($"[WebUiHost] start skipped: {e.Message}");
            }

            RemoteExecActivation.ActivateIfRequested(Client.WebUiHost.Boot.WebUiHost.KestrelPort);
        }
    }
}
