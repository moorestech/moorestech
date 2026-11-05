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

            // 接続情報を書けなければ誰も入口へ届かないので、外から見えない公開待ちで確かめ、書けなければ Harmony を読む前に入口を閉じる
            // Without written access details nobody can reach the entry, so check with an invisible staged file and close the entry before loading Harmony when it fails
            if (!RemoteExecAccessFile.Stage(port.Value))
            {
                Deactivate();
                Debug.LogError("[RemoteExec] 接続情報を書けなかったため入口を閉じました（遠隔実行は使えません）");
                return;
            }
            RemoteExecHarmonyLoader.Load();

            // 入口は Harmony を読み終えてから見せる。先に見えると、読み込み前に届いた要求が Harmony 無しでコンパイルされる
            // Reveal the entry only after Harmony has loaded; otherwise a request arriving earlier compiles without Harmony
            if (!RemoteExecAccessFile.Publish())
            {
                Deactivate();
                Debug.LogError("[RemoteExec] 接続情報を公開できなかったため入口を閉じました（遠隔実行は使えません）");
            }
        }

        // 入口（待受と接続情報）の撤去はここ1つに集める。呼ぶのは終了イベントの購読者・無効な起動の判定・有効化の途中失敗。Editorの後片付けは待受のソケットだけを閉じる
        // Withdrawing the entry (listener and access details) lives here alone, called by the shutdown subscriber, the disabled-boot resolution and a failed activation; the Editor cleanup closes only the listener socket
        public static void Deactivate()
        {
            RemoteExecHttpListener.Stop();
            RemoteExecAccessFile.Remove();
        }
    }
}
