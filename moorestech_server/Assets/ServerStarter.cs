using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Server.Boot;
using UnityEngine;

public class ServerStarter : MonoBehaviour
{
    private Thread _serverUpdateThread;
    private Thread _gameUpdateThread;
    private CancellationTokenSource _autoSaveToken;
    void Start()
    {
        (_serverUpdateThread,_gameUpdateThread,_autoSaveToken) = StartServer.Start(new string[]{});
        _serverUpdateThread.Start();
        _gameUpdateThread.Start();
    }

    private void OnDestroy()
    {
        _serverUpdateThread.Abort();
        _gameUpdateThread.Abort();
        _autoSaveToken.Cancel();
    }
}
