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
            Debug.Log("Shutting down server");
            _startServer.Dispose();
            Debug.Log("Server shut down");
        }
    }
}