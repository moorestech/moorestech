using Client.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter
{
    // ローカルゲーム開始（メニュー・イベント共通）
    // Local game start shared by menu and event mode
    public static class LocalGameLauncher
    {
        public static void StartLocalGame()
        {
            // 多重呼び出しでも購読が1本に収まるよう先に外しておく
            // Unsubscribe first so repeated calls never leave duplicate subscriptions
            SceneManager.sceneLoaded -= OnGameInitializerSceneLoaded;
            SceneManager.sceneLoaded += OnGameInitializerSceneLoaded;
            SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
        }

        private static void OnGameInitializerSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneConstant.GameInitializerSceneName) return;

            ApplyInitialProperties();

            #region Internal

            void ApplyInitialProperties()
            {
                SceneManager.sceneLoaded -= OnGameInitializerSceneLoaded;
                var starter = Object.FindObjectOfType<InitializeScenePipeline>();
                var playerId = PlayerPrefs.HasKey(PlayerPrefsKeys.PlayerIdKey) ? PlayerPrefs.GetInt(PlayerPrefsKeys.PlayerIdKey) : (int?)null;
                starter.SetProperty(InitializeProprieties.CreateLocalServer(playerId));
            }

            #endregion
        }
    }
}
