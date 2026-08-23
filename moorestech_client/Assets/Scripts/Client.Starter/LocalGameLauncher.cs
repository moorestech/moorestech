using Client.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter
{
    // ローカルゲーム開始フロー（メニューのボタンとイベントモード自動開始の共通経路）
    // Local game start flow shared by the menu button and event-mode auto start
    public static class LocalGameLauncher
    {
        public static void StartLocalGame()
        {
            SceneManager.sceneLoaded += OnGameInitializerSceneLoaded;
            SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
        }

        private static void OnGameInitializerSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnGameInitializerSceneLoaded;
            var starter = Object.FindObjectOfType<InitializeScenePipeline>();
            starter.SetProperty(InitializeProprieties.CreateLocalServer(PlayerPrefs.GetInt(PlayerPrefsKeys.PlayerIdKey)));
        }
    }
}
