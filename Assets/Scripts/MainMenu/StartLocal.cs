using System.Diagnostics;
using GameConst;
using MainGame.Basic;
using MainGame.Starter;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace MainMenu
{
    public class StartLocal : MonoBehaviour
    {
        [SerializeField] private Button startLocalButton;
            

        private void Start()
        {
            startLocalButton.onClick.AddListener(StartLocalServer);
        }

        private Process _serverProcess;
        private void StartLocalServer()
        {
            _serverProcess = new Process();
            _serverProcess.StartInfo.FileName = ServerConst.ServerExePath;
#if UNITY_EDITOR_WIN
            _serverProcess.StartInfo.Arguments = ServerConst.ServerModsDirectory;
#elif UNITY_STANDALONE_WIN
            _serverProcess.StartInfo.Arguments ="startupFromClient";
#endif
            _serverProcess.Start();

            SceneManager.sceneLoaded += OnMainGameSceneLoaded;
            SceneManager.LoadScene(SceneConstant.MainGameSceneName);
        }
        
        private void OnMainGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnMainGameSceneLoaded;
            var starter = GameObject.FindObjectOfType<MainGameStarter>();

            var isLocal = true;
            
            starter.SetProperty(new MainGameStartProprieties(
                isLocal,_serverProcess,
                ServerConst.LocalServerIp,
                ServerConst.LocalServerPort,
                PlayerPrefs.GetInt(PlayerPrefsKeys.PlayerIdKey)));
        }
        
    }
}