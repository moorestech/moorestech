using System;
using UnityEngine;
using UnityEngine.UI;

namespace MainMenu
{
    public class StartLocal : MonoBehaviour
    {
        [SerializeField] private Button startLocalButton;
            
#if UNITY_EDITOR_WIN
        private const string ServerExePath = "./Server/Server.exe";
#endif

        private void Start()
        {
            startLocalButton.onClick.AddListener(StartLocalServer);
        }

        private void StartLocalServer()
        {
            Debug.Log(System.IO.Path.GetFullPath(ServerExePath));
        }
        
    }
}