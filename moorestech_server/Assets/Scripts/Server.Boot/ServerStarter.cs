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
        
        private void Awake()
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