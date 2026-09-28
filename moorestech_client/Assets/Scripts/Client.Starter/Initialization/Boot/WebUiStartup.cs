using System;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.Initialization.Boot
{
    internal static class WebUiStartup
    {
        internal static async UniTask<bool> StartAsync(CancellationToken exitToken)
        {
            // 終了時の購読はWebUiHostが一度だけ張る。UIはWeb一本なので起動失敗時は非表示で続ける
            // WebUiHost owns its single shutdown subscription; the web-only UI stays hidden if startup fails
            // 外部プロセスの起動境界を隔離し、ゲームの初期化を続ける
            // Isolate external process startup failures and continue game initialization
            try
            {
                var started = await Client.WebUiHost.Boot.WebUiHost.StartAsync(exitToken);
                if (!started) Debug.LogWarning("[WebUiHost] 起動が完了せず、遠隔実行も開きません");
                return started;
            }
            catch (Exception e) when (e is IOException || e is SocketException || e is Win32Exception || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                Debug.LogWarning($"[WebUiHost] 外部プロセスまたはHTTP待受の起動に失敗しました: {e}");
                return false;
            }
        }
    }
}
