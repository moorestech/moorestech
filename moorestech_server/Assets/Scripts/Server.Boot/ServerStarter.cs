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
        public async UniTask ShutdownAsync()
        {
            // 終了時点の世界を必ず1回書き出させる。通信経由のSaveと違い到達待ちが要らない
            // Force one final write of the world; unlike the networked Save it needs no arrival wait
            _startServer?.RequestSave();

            for (var frame = 0; frame < SaveFlushWaitFrameLimit && _startServer != null && _startServer.HasPendingSave; frame++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            // 破棄はOnDestroy経由に一本化する
            // Funnel the teardown through OnDestroy
            Destroy(gameObject);
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
            Debug.Log("サーバーを終了します");
            _startServer?.Dispose();
            Debug.Log("サーバーを終了しました");
        }
    }
}
