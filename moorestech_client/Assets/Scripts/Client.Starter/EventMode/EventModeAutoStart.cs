using Client.Common;
using Client.Localization;
using Game.Paths;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter.EventMode
{
    // 起動時にワールド削除・英語化・自動開始
    // On boot: delete world, reset to English, auto-start
    public static class EventModeAutoStart
    {
        // 起動フックから切り離した発火条件。ワールド削除の是非をここだけで決める
        // The run condition, split from the boot hook, is the single place deciding whether the world gets wiped
        public static bool ShouldRun(EventExhibitionSettings settings, string activeSceneName)
        {
            if (!settings.IsEnabled) return false;
            // メインメニュー以外では何もしない
            // Do nothing outside the main menu
            return activeSceneName == SceneConstant.MainMenuSceneName;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void AutoStartIfEventMode()
        {
            var settings = EventExhibitionSettings.FromEnvironment();
            if (!ShouldRun(settings, SceneManager.GetActiveScene().name)) return;

            // 新規生成（PlayerPrefs維持）
            // Regenerate world; PlayerPrefs kept
            GameSystemPaths.DeleteDefaultWorldDirectory();
            if (!Localize.TrySetLanguage(Localize.DefaultLanguageCode)) Debug.LogError($"EventModeAutoStart: failed to set language to {Localize.DefaultLanguageCode}");
            LocalGameLauncher.StartLocalGame();
        }
    }
}
