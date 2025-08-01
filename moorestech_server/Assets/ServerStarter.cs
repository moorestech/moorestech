using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Update;
using Server.Boot;
using UnityEngine;

public class ServerStarter : MonoBehaviour
{
    private CancellationTokenSource _autoSaveToken;
    private Thread _connectionUpdateThread;
    private Task _gameUpdaterThread;

    private void Start()
    {
        (_connectionUpdateThread, _gameUpdaterThread, _autoSaveToken) = StartServer.Start(new string[] { });
        _connectionUpdateThread.Start();
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
        _gameUpdaterThread?.Dispose();
        _connectionUpdateThread?.Abort();
        _autoSaveToken?.Cancel();
        GameUpdater.Dispose();
        Debug.Log("サーバーを終了しました");
    }
}