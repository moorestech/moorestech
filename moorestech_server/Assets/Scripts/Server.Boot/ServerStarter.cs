using System;
using UnityEngine;

namespace Server.Boot
{
    public class ServerStarter : MonoBehaviour
    {
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
            _startServer.Dispose();
            Debug.Log("サーバーを終了しました");
        }
    }
}