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
    }
}
