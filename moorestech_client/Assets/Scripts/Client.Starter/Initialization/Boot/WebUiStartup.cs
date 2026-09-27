using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.Initialization.Boot
{
    internal static class WebUiStartup
    {
        internal static async UniTask StartAsync(CancellationToken exitToken)
        {
            // 終了時の購読はWebUiHostが一度だけ張る。UIはWeb一本なので起動失敗時は非表示で続ける
            // WebUiHost owns its single shutdown subscription; the web-only UI stays hidden if startup fails
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
        }
    }
}
