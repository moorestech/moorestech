using UnityEngine;

namespace Server.Boot
{
    public class ServerStarter : MonoBehaviour
    {
        private readonly ServerInstanceManager _startServer = new(new string[] { });
        
        private void Awake()
        {
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