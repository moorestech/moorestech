using System.Diagnostics;
using GameConst;
using MainGame.Basic;
using MainGame.Starter;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        private Process _serverProcess;
        private void StartLocalServer()
        {
            _serverProcess = new Process();
            _serverProcess.StartInfo.FileName = GetFullPath(ServerExePath);
            _serverProcess.StartInfo.Arguments = $"{GetFullPath(ServerConfigPath)}";
            _serverProcess.Start();

            SceneManager.sceneLoaded += OnMainGameSceneLoaded;
            SceneManager.LoadScene(SceneConstant.MainGameSceneName);
        }
        
        private void OnMainGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnMainGameSceneLoaded;
            var starter = GameObject.FindObjectOfType<Starter>();

            var isLocal = true;
            
            starter.SetProperty(new MainGameStartProprieties(
                isLocal,_serverProcess,
                ServerConst.LocalServerIp,
                ServerConst.LocalServerPort,
                PlayerPrefs.GetInt(PlayerPrefsKeys.PlayerIdKey)));
        }
        
    }
}