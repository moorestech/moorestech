using System.Threading;
using Server.Boot;
using Server.Core.Update;
using UnityEngine;

public class ServerStarter : MonoBehaviour
{
    private CancellationTokenSource _autoSaveToken;
    private Thread _serverUpdateThread;

    private void Start()
    {
        (_serverUpdateThread, _autoSaveToken) = StartServer.Start(new string[] { });
        _serverUpdateThread.Start();
    }

    private void FixedUpdate()
    {
        GameUpdater.Update();
    }

    private void OnDestroy()
    {
        _serverUpdateThread.Abort();
        _autoSaveToken.Cancel();
        GameUpdater.Dispose();
    }
}