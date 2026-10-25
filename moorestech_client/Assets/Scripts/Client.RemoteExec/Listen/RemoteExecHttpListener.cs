using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.RemoteExec.Listen
{
    // 遠隔実行専用の待受。Web UIのホストとは独立に開閉する（ADR 0072 2026-10-26改訂）
    // The remote-exec-only listener, opened and closed independently of the Web UI host (ADR 0072, revised 2026-10-26)
    internal static class RemoteExecHttpListener
    {
        // Web UIのKestrel(25050〜)・Vite(25173〜)と重ならない帯
        // A band that overlaps neither the Web UI Kestrel (25050+) nor Vite (25173+)
        internal const int BasePort = 25100;
        internal const int PortSearchRange = 20;

        private static HttpListener _listener;
        private static CancellationTokenSource _lifetime;

        internal static int? Start()
        {
            // Editorの再生し直しで前回の待受が残っていても二重に開かない
            // Never open twice even if an Editor replay left the previous listener open
            Stop();

            // 実行環境がHttpListenerを持たなければポートを変えても開けない。理由を残して諦める
            // Without HttpListener support no port will open, so give up with a reason
            if (!HttpListener.IsSupported)
            {
                Debug.LogError("[RemoteExec] この実行環境は HttpListener に対応していないため待受を開けません（遠隔実行は使えません）");
                return null;
            }

            // 基準ポートから上へbindを試し、最初に開けたポートを採る（KestrelServerと同じ探索）
            // Probe upward from the base port and adopt the first that binds (the same search as KestrelServer)
            for (var port = BasePort; port < BasePort + PortSearchRange; port++)
            {
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                // OSのネットワーク境界。使用中ポートのbind失敗だけを隔離して次を試す
                // OS network boundary; only an occupied-port bind failure is isolated before trying the next port
                try
                {
                    listener.Start();
                }
                catch (Exception e) when (e is HttpListenerException || e is SocketException)
                {
                    listener.Close();
                    Debug.Log($"[RemoteExec] ポート {port} は使えないため次を試します: {e.Message}");
                    continue;
                }

                _listener = listener;
                _lifetime = new CancellationTokenSource();
                AcceptLoopAsync(listener, _lifetime.Token).Forget();
                Debug.Log($"[RemoteExec] 待受を開きました http://127.0.0.1:{port}");
                return port;
            }

            Debug.LogError($"[RemoteExec] 空きポートが無いため待受を開けません（遠隔実行は使えません） {BasePort}..{BasePort + PortSearchRange - 1}");
            return null;
        }

        internal static void Stop()
        {
            // silent-ok: 待受未作成・停止済みの冪等な操作で解放対象が無い / 開いた待受の停止は下のログで開発者に届く
            if (_listener == null) return;
            _lifetime.Cancel();
            _listener.Close();
            _listener = null;
            _lifetime = null;
            Debug.Log("[RemoteExec] 待受を閉じました");
        }

        // 受付はスレッドプールで回す。メインスレッドが止まっていてもserver実行の要求は受け取れる
        // Accept on the thread pool so server-target requests still arrive while the main thread is stalled
        private static async UniTaskVoid AcceptLoopAsync(HttpListener listener, CancellationToken lifetime)
        {
            while (!lifetime.IsCancellationRequested)
            {
                HttpListenerContext context;
                // OSのネットワーク境界。停止による中断と想定外の故障を分けて扱う
                // OS network boundary; a stop-induced interruption is told apart from an unexpected fault
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception e) when (e is HttpListenerException || e is ObjectDisposedException || e is InvalidOperationException)
                {
                    // silent-ok: Stop() による中断で、閉じたことは Stop() が「待受を閉じました」でログ済み / 開発者が Player.log で気づく
                    if (lifetime.IsCancellationRequested) return;
                    Debug.LogError($"[RemoteExec] 待受が要求を受け取れなくなりました（遠隔実行は使えません。再起動で復旧）: {e}");
                    return;
                }
                RemoteExecHttpExchange.HandleAsync(context, lifetime).Forget(e => Debug.LogError($"[RemoteExec] 要求の処理に失敗しました: {e}"));
            }
        }
    }
}
