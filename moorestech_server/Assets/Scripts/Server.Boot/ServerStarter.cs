using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Server.Boot
{
    public class ServerStarter : MonoBehaviour
    {
        // 保存の書き出しを待つ上限フレーム数。届かなくても終了不能にしない
        // Frame budget for the save flush; shutdown must never become impossible
        private const int SaveFlushWaitFrameLimit = 600;

        private ServerInstanceManager _startServer;
        private string[] _args = Array.Empty<string>();

        // Unityの破棄経路（OnApplicationQuit・OnDestroy）がサーバーを畳み終えたか。Play停止や強制終了ではShutdownAsyncの待ちより先に来る
        // Whether Unity's teardown (OnApplicationQuit/OnDestroy) already folded the server; on a play stop or forced quit it arrives before ShutdownAsync's wait ends
        private bool _serverFinished;

        // サーバーが実際にバインドしたポート。起動完了まで0
        // The port the server actually bound; 0 until startup completes
        public int BoundPort => _startServer?.BoundPort ?? 0;

        public void SetArgs(string[] args)
        {
            _args = args;
        }
        
        private void Start()
        {
            _startServer = new ServerInstanceManager(_args);
            _startServer.Start();
        }
        
        // 保留中の保存を消化してから畳む唯一の終了契約。破棄側は必ずここを通る
        // The single shutdown contract that flushes pending saves before folding; every teardown goes through here
        public async UniTask<ServerSaveFlushResult> ShutdownAsync()
        {
            // 終了時点の世界を必ず1回書き出させる。通信経由のSaveと違い到達待ちが要らない。畳み終えたサーバーには要求しない（取り込む者がおらず永久に保留になる）
            // Force one final write of the world; unlike the networked Save it needs no arrival wait. A folded server is not asked, since nobody would capture it and it would stay pending forever
            if (!_serverFinished) _startServer?.RequestSave();

            // 破棄経路が先に畳んだら、終了時の世界はその中で書き出し済みなので待ちを打ち切る
            // If the teardown folded the server first, it already wrote the final world, so stop waiting
            for (var frame = 0; frame < SaveFlushWaitFrameLimit && !_serverFinished && _startServer != null && _startServer.HasPendingSave; frame++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            var flushResult = ResolveFlushResult();

            // 破棄経路が先に来たときは自分のGameObjectはもう無い。受付の停止も破棄もそちらで済んでいる
            // When the teardown came first this GameObject is already gone; admission and destruction were both handled there
            if (_serverFinished) return flushResult;

            // 終了完了を返す前に受付を閉じ、遅延破棄までの実行を防ぐ
            // Close admission before reporting shutdown, ahead of deferred destruction
            _startServer?.StopThreadActions();

            // 破棄はOnDestroy経由に一本化する
            // Funnel the teardown through OnDestroy
            Destroy(gameObject);
            return flushResult;

            #region Internal

            ServerSaveFlushResult ResolveFlushResult()
            {
                if (_startServer == null) return ServerSaveFlushResult.Flushed;

                // 上限到達は「書き出しきれていない」ため完了と区別して返す
                // Hitting the budget means the write never finished, so report it apart from completion
                if (_startServer.HasPendingSave) return ServerSaveFlushResult.FlushTimedOut;

                // 諦めは待ちが明けるだけで世界は保存されていない。無音で成功にするとプレイヤーは保存済みと信じる
                // A give-up merely clears the wait while the world stays unsaved; a silent success would let the player believe it was saved
                if (_startServer.HasAbandonedSave)
                {
                    Debug.LogError("終了時のセーブは書き出しに繰り返し失敗して諦めたため、世界は保存されていません");
                    return ServerSaveFlushResult.SaveAbandoned;
                }

                return ServerSaveFlushResult.Flushed;
            }

            #endregion
        }
        
        private void OnDestroy()
        {
            FinishServer();
        }
        
        private void OnApplicationQuit()
        {
            FinishServer();
        }
        
        private void FinishServer()
        {
            // OnApplicationQuitとOnDestroyの両方から来るので、2回目は畳まない
            // Both OnApplicationQuit and OnDestroy arrive here, so the second one does not fold again
            if (_serverFinished) return;
            _serverFinished = true;
            Debug.Log("サーバーを終了します");

            // Play停止・強制終了ではクライアントの終了表明より先にここへ来ることがある。畳んだ後の要求は誰も書けないので、終了時の世界はここで要求しDisposeに書き出させる
            // On a play stop or forced quit this can run before the client's shutdown declaration; nobody can write a request after folding, so request the final world here and let Dispose write it
            _startServer?.RequestSave();
            _startServer?.Dispose();
            Debug.Log("サーバーを終了しました");
        }
    }
}
