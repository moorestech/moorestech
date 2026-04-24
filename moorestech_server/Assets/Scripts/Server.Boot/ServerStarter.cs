using System;
using UnityEngine;

namespace Server.Boot
{
    public class ServerStarter : MonoBehaviour
    {
        private ServerInstanceManager _startServer;
        private string[] _args = Array.Empty<string>();

        public void SetArgs(string[] args)
        {
            _args = args;
        }

        private void Start()
        {
            _startServer = new ServerInstanceManager(_args);
            _startServer.Start();
        }

        // シーン破棄時に同期でサーバースレッドと GameUpdater を落とす。
        // Bridge 経由の Coordinator は Editor quit 等の MonoBehaviour が壊れない経路用フォールバック
        // Synchronously tear down server threads and GameUpdater on scene destroy.
        // Coordinator via Bridge is a fallback for paths where MonoBehaviour lifecycle is skipped
        private void OnDestroy()
        {
            _startServer?.ShutdownNow();
        }
    }
}
