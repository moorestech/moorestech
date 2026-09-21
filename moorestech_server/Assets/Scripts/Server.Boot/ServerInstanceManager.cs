using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Core.Update;
using Game.SaveLoad;
using Game.SaveLoad.Snapshot;
using UnityEngine;

namespace Server.Boot
{
    public class ServerInstanceManager : IDisposable
    {
        private Thread _connectionUpdateThread;
        private Thread _gameUpdateThread;
        private CancellationTokenSource _cancellationTokenSource;
        private Socket _listener;

        // 終了時に保留中の保存を消化するための保存調停役
        // The save coordinator used to flush pending saves at shutdown
        private WorldSaveCoordinator _worldSaveCoordinator;

        // 終了時に常時記録のOSリソースを手放すための参照
        // Reference used to release always-on capture's OS resources at shutdown
        private WorldSnapshotRing _worldSnapshotRing;

        private readonly string[] _args;

        // 実際にバインドされた待ち受けポート。バインド前は0
        // The actually bound listen port; 0 before binding
        public int BoundPort => _listener == null ? 0 : ((IPEndPoint)_listener.LocalEndPoint).Port;

        public ServerInstanceManager(string[] args)
        {
            _args = args;
        }

        // 要求済みの保存が書き出し待ちで残っているか
        // Whether a requested save is still waiting to be written
        public bool HasPendingSave => _worldSaveCoordinator != null && _worldSaveCoordinator.HasPendingSave;

        // 書き出しを諦めた保存があるか。諦めも待ちを明けるので、これを見ないと終了が成功を名乗る
        // Whether a save gave up writing; a give-up also clears the wait, so without this the shutdown claims success
        public bool HasAbandonedSave => _worldSaveCoordinator != null && _worldSaveCoordinator.HasAbandonedSave;

        public void Start()
        {
            (_connectionUpdateThread, _gameUpdateThread, _cancellationTokenSource, _listener) = ServerInstanceStartup.Start(_args, out _worldSaveCoordinator, out _worldSnapshotRing);
        }

        // 終了直前の保存を通信を介さず直接要求する。パケット到達待ちの競合を作らない
        // Request the shutdown save in-process so no packet-arrival race is created
        public void RequestSave()
        {
            _worldSaveCoordinator?.RequestSave();
        }

        public void Dispose()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            try
            {
                WaitForThread(_connectionUpdateThread, "通信受け入れ");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            try
            {
                WaitForThread(_gameUpdateThread, "ゲーム更新");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            // AcceptとCloseを競合させず、両thread停止後に通信資源を閉じる
            // Close network resources after both threads stop so Accept never races Close
            // ソケット破棄は外部境界。例外を隔離して記録し、保存と記録資源の後始末を継続する
            // Socket disposal is an external boundary; isolate and log failures so save and capture cleanup can continue
            try
            {
                _listener?.Close();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            // tickスレッドを止めた後に保留中の保存を消化する。ここを飛ばすと最後の数十秒が無言で消える
            // Drain pending saves after the tick thread stops; skipping this silently loses the last tens of seconds
            // この待ちの先はディスクへ書く外部境界（書き出しスレッドとファイルハンドル）なので、例外を隔離して残りの後始末を続ける
            // The wait ends at the disk boundary (the writer thread and its file handles), so an exception is isolated to let the rest of the teardown continue
            try
            {
                // 待ち切れなかったことを終了経路の側でも残す。書き出し側のログだけではどの待ちが明けなかったか分からない
                // Record the timeout on the shutdown path too; the writer's own log does not say which wait failed to clear
                if (_worldSaveCoordinator != null && !_worldSaveCoordinator.WaitForPendingWrites()) Debug.LogError("終了時のセーブ書き出しを待ち切れませんでした");
            }
            catch (Exception e)
            {
                // 握った例外は必ず理由を残す。無音だと「保存された」と見分けがつかない
                // An exception caught here always leaves its reason; silence would be indistinguishable from a saved world
                Debug.LogError($"終了時のセーブ書き出し待ちが例外で終わったため、世界は保存されていない可能性があります message:{e.Message}");
                Debug.LogException(e);
            }
            // 常時記録が持つ書き出しスレッドと区間ファイルのハンドルを手放す。残すとセッション毎に積み上がる
            // Release the writer thread and segment file handle always-on capture holds; leaving them accumulates per session
            // ここもファイルハンドルを閉じる外部境界。閉じ損ねても残りの後始末（GameUpdater）まで到達させる
            // This too is the file-handle boundary; even a failed close must not stop the remaining teardown (GameUpdater)
            try
            {
                _worldSnapshotRing?.Stop();
            }
            catch (Exception e)
            {
                // 常時記録のハンドルが残ったまま止まったことを残す。次セッションの区間切り替え失敗はここが原因になる
                // Record that capture stopped with handles still held; it is the cause of the next session's segment rotation failures
                Debug.LogError($"常時記録の停止が例外で終わったため、区間ファイルのハンドルが残っている可能性があります message:{e.Message}");
                Debug.LogException(e);
            }
            try
            {
                GameUpdater.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            #region Internal

            static void WaitForThread(Thread thread, string label)
            {
                if (thread == null || !thread.IsAlive) return;
                if (!thread.Join(TimeSpan.FromSeconds(5)))
                    Debug.LogError($"{label}threadがcancel後5秒で停止しませんでした。終了処理完了後もthreadが残る可能性があります");
            }

            #endregion
        }
    }
}
