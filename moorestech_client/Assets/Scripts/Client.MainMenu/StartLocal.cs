using System.Diagnostics;
using Client.Starter;
using Cysharp.Threading.Tasks;
using GameConst;
using Constant;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace MainMenu
{
    public class StartLocal : MonoBehaviour
    {
        [SerializeField] private Button startLocalButton;

        private Process _serverProcess;


        private void Start()
        {
            startLocalButton.onClick.AddListener(() => StartLocalServer().Forget());
        }

        private async UniTask StartLocalServer()
        {
            _serverProcess = new Process();
            _serverProcess.StartInfo.FileName = ServerConst.DotnetRuntimePath;
            _serverProcess.StartInfo.Arguments = $"\"{ServerConst.ServerDllPath}\"";
            _serverProcess.StartInfo.UseShellExecute = true;
            _serverProcess.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;

            Debug.Log($"Start Server Runtime : {ServerConst.DotnetRuntimePath} Arguments : {ServerConst.ServerDllPath}");
            _serverProcess.Start();
            await UniTask.Delay(1000);
            if (_serverProcess.HasExited)
            {
                Debug.LogError("Server did not start");
                Debug.LogError($"ExitCode : {_serverProcess.ExitCode}");
                Debug.LogError("Log : " + _serverProcess.StandardOutput.ReadToEnd());
                Debug.LogError("Message : " + _serverProcess.StandardError.ReadToEnd());
                return;
            }

            Debug.Log("Server started");

            SceneManager.sceneLoaded += OnMainGameSceneLoaded;
            SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
        }

        private void OnMainGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnMainGameSceneLoaded;
            var starter = FindObjectOfType<InitializeScenePipeline>();

            starter.SetProperty(new InitializeProprieties(
                true, _serverProcess,
                ServerConst.LocalServerIp,
                ServerConst.LocalServerPort,
                PlayerPrefs.GetInt(PlayerPrefsKeys.PlayerIdKey)));
        }
    }
}