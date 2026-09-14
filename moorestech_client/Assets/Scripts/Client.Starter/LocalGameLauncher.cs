using Client.Common;
using Client.PlaytestReceiver.Gate;
using Server.Boot;
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
            // プレイテスト配布版で照合に落ちていたら開始しない（ADR 0058・オンライン必須）
            // A distribution build that failed the playtest check never starts (ADR 0058, online required)
            if (PlaytestLaunchGate.RejectStart(nameof(StartLocalGame))) return;

            // 本番のプレイ開始だけが常時記録を有効にする。テスト・プレイテスト・QAは既定の無効のまま走る
            // Only the real play start enables always-on capture; tests, playtests and QA run on the disabled default
            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Enabled());

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
