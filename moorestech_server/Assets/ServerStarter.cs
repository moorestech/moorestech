using System;
using System.Threading;
using Core.Update;
using Server.Boot;
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
        FinishServer();
    }
    
    private void OnApplicationQuit()
    {
        FinishServer();
    }
    
    private void FinishServer()
    {
        Debug.Log("サーバーを終了します");
        _serverUpdateThread?.Abort();
        _autoSaveToken?.Cancel();
        GameUpdater.Dispose();
        Debug.Log("サーバーを終了しました");
    }
}