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
        public static void ApplyLaunchLanguage(EventExhibitionSettings settings)
        {
            var requestedLanguageCode = settings.RequestedLanguageCode;

            // 未指定は既定言語を適用して正常扱い
            // Unset applies the default language and counts as normal
            if (string.IsNullOrEmpty(requestedLanguageCode))
            {
                ApplyDefaultLanguage();
                return;
            }

            // 可否判定はTrySetLanguage（公開辞書）だけに任せる
            // Acceptance is decided only by TrySetLanguage against the published dictionary
            if (Localize.TrySetLanguage(requestedLanguageCode)) return;

            Debug.LogError($"EventModeAutoStart: unknown {EventExhibitionSettings.LanguageEnvKey}={requestedLanguageCode}, falling back to {Localize.DefaultLanguageCode}");
            ApplyDefaultLanguage();

            #region Internal

            void ApplyDefaultLanguage()
            {
                // 既定言語の適用失敗も握り潰さない
                // A failed default apply is never swallowed either
                if (!Localize.TrySetLanguage(Localize.DefaultLanguageCode))
                    Debug.LogError($"EventModeAutoStart: failed to set language to {Localize.DefaultLanguageCode}");
            }

            #endregion
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
            LocalGameLauncher.StartLocalGame();
        }
    }
}
