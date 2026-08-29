using Client.Common;
using Client.Localization;
using Game.Paths;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter.EventMode
{
    // 起動時にワールド削除・起動言語の適用・自動開始
    // On boot: delete world, apply the launch language, auto-start
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

        // 未知の言語コードはログだけ残し起動は止めない
        // An unknown language code only logs and never stops boot
        public static LanguageApplyResult ApplyLaunchLanguage(EventExhibitionSettings settings)
        {
            var result = LocalizeLanguageApplier.ApplyOrDefault(settings.RequestedLanguageCode);
            if (result.Resolution == LanguageResolution.UnknownFallback)
                Debug.LogError($"EventModeAutoStart: unknown MOORESTECH_EVENT_LANGUAGE={settings.RequestedLanguageCode}, falling back to {result.AppliedLanguageCode}");
            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void AutoStartIfEventMode()
        {
            var settings = EventExhibitionSettings.FromEnvironment();
            if (!ShouldRun(settings, SceneManager.GetActiveScene().name)) return;

            // 新規生成（PlayerPrefs維持）
            // Regenerate world; PlayerPrefs kept
            GameSystemPaths.DeleteDefaultWorldDirectory();
            ApplyLaunchLanguage(settings);
            EventIdleQuitWatcher.Create(settings.IdleTimeoutSeconds);
            LocalGameLauncher.StartLocalGame();
        }
    }
}
