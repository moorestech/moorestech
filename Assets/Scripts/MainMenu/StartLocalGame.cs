using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace MainMenu
{
    public class StartLocalGame : MonoBehaviour
    {
        [SerializeField] private Button startLocal;
        
#if UNITY_EDITOR_WIN
        private const string ServerExePath = "./Server/Server.exe";
#elif UNITY_EDITOR_OSX
        private const string ServerExePath = "";
#elif UNITY_STANDALONE_WIN
        private const string ServerExePath = "/Server/";
#elif UNITY_STANDALONE_OSX
        private const string ServerExePath = "";
#endif
        private void Awake()
        {
            startLocal.onClick.AddListener(StartGame);
        } 

        private void StartGame()
        {
        }
    }
}