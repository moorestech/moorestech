using Client.RemoteExec.Access;
using Client.RemoteExec.Listen;
using Client.RemoteExec.Loading;
using UnityEngine;

namespace Client.RemoteExec
{
    // 有効な起動だけで専用の待受・Harmony・接続情報を用意する。Web UIの起動結果には依らない
    // Prepare the dedicated listener, Harmony and access details only on enabled boots, independent of the Web UI startup
    public static class RemoteExecActivation
    {
        public static void ActivateIfRequested()
        {
            if (!RemoteExecLaunchOption.IsEnabled)
            {
                Debug.Log("[RemoteExec] 起動オプションが無いため遠隔実行は無効です");
                return;
            }
            // 入口を開けるかはHarmonyを読む前に決める。読んだ後で断ると、パッチ能力だけ得て入口が永久に閉じた状態が残る
            // Whether the entry can open is decided before loading Harmony; refusing afterwards would leave patching enabled with the entry shut forever
            var port = RemoteExecHttpListener.Start();
            // silent-ok: 開けない理由は Start() が LogError 済み / 開発者が Player.log と remote-exec.sh の接続失敗で気づく
            if (port == null) return;

            // DLLを読み接続情報を発行
            // Load the DLL and publish access details
            RemoteExecHarmonyLoader.Load();
            RemoteExecAccessFile.Issue(port.Value);
        }

        // 入口（待受と接続情報）の撤去はここ1つに集める。呼ぶのは終了イベントの購読者と無効な起動の判定。Editorの後片付けは待受のソケットだけを閉じる
        // Withdrawing the entry (listener and access details) lives here alone, called by the shutdown subscriber and the disabled-boot resolution; the Editor cleanup closes only the listener socket
        public static void Deactivate()
        {
            RemoteExecHttpListener.Stop();
            RemoteExecAccessFile.Remove();
        }
    }
}
