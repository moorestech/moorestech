using System;
using System.Threading;
using Client.Common;
using Client.Game.Common;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter.Initialization.Scene
{
    /// <summary>
    ///     主シーン読込後の初期化と失敗表示を引き受ける
    ///     Starts main-scene initialization and reports failures after load
    /// </summary>
    public class MainGameSceneLoadedHandler
    {
        private readonly ServerConnectionResult _serverResult;
        private readonly string _serverDirectory;
        private readonly bool _collectsPlaytestRecords;
        private readonly CancellationToken _exitToken;

        public MainGameSceneLoadedHandler(ServerConnectionResult serverResult, string serverDirectory, bool collectsPlaytestRecords, CancellationToken exitToken)
        {
            _serverResult = serverResult;
            _serverDirectory = serverDirectory;
            _collectsPlaytestRecords = collectsPlaytestRecords;
            _exitToken = exitToken;
        }

        public void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            // Forget境界の例外を受け、初期化失敗をログとメニュー遷移へ出す
            // Observe forgotten failures and surface them in log and menu transition
            new MainGameInitializationFinalizer(_serverResult, _serverDirectory, _collectsPlaytestRecords).RunAsync(_exitToken).Forget(exception =>
            {
                if (exception is OperationCanceledException)
                {
                    Debug.Log("Initialization was aborted because an exit cancellation arrived midway");
                    return;
                }

                Debug.LogError($"初期化処理中にエラーが発生しました: {exception.GetType()} {exception.Message}\n{exception.StackTrace}");
                GameShutdownEvent.FireGameShutdown(GameShutdownReason.InitializationFailed);
                SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
            });
        }
    }
}
