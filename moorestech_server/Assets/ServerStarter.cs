using System.Threading;
using Core.Update;
using Server.Boot;
using UnityEngine;

public class ServerStarter : MonoBehaviour
{
    private CancellationTokenSource _autoSaveToken;
    private Thread _gameUpdateThread;
    private Thread _serverUpdateThread;

    private void Start()
    {
        (_serverUpdateThread, _gameUpdateThread, _autoSaveToken) = StartServer.Start(new string[] { });
        _serverUpdateThread.Start();
        _gameUpdateThread.Start();
    }

    private void OnDestroy()
    {
        _serverUpdateThread.Abort();
        _gameUpdateThread.Abort();
        _autoSaveToken.Cancel();
        GameUpdater.Dispose();
    }
}