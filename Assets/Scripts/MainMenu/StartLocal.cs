using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using static System.IO.Path;

namespace MainMenu
{
    public class StartLocal : MonoBehaviour
    {
        [SerializeField] private Button startLocalButton;
            
#if UNITY_EDITOR_WIN
        private const string ServerExePath = "./WindowsServer/moorestech_server.exe";
        private const string ServerConfigPath = "./WindowsServer/Config";
#endif

        private void Start()
        {
            startLocalButton.onClick.AddListener(StartLocalServer);
        }

        private void StartLocalServer()
        {
            Process server = new Process();
            server.StartInfo.FileName = GetFullPath(ServerExePath);
            server.StartInfo.Arguments = $"{GetFullPath(ServerConfigPath)}";
            server.Start();
        }
        
    }
}