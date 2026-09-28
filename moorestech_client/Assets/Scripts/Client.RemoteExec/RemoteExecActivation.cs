using Client.RemoteExec.Access;
using Client.RemoteExec.Loading;
using UnityEngine;

namespace Client.RemoteExec
{
    // Web UI起動後、有効な起動だけでHarmonyと接続情報を用意する
    // Prepare Harmony and access details only for enabled boots after Web UI startup
    public static class RemoteExecActivation
    {
        public static void ActivateIfRequested(bool webUiStarted, int? kestrelPort)
        {
            if (!RemoteExecLaunchOption.IsEnabled)
            {
                Debug.Log("[RemoteExec] 起動オプションが無いため遠隔実行は無効です");
                return;
            }
            if (!webUiStarted)
            {
                Debug.LogError("[RemoteExec] Web UI サーバーが起動していないため遠隔実行を開けません");
                return;
            }

            // DLLを読み接続情報を発行
            // Load the DLL and publish access details
            RemoteExecHarmonyLoader.Load();
            RemoteExecAccessFile.Issue(kestrelPort);
        }

        // 正常終了で入口を撤去する。呼び出し側（終了イベントの購読者）が唯一の発火点
        // A clean exit withdraws the entry; the caller subscribing to the shutdown event is its only trigger
        public static void Deactivate()
        {
            RemoteExecAccessFile.Remove();
        }
    }
}
