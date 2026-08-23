using System.IO;
using Client.Common;
using Client.Localization;
using Server.Boot;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter.EventMode
{
    // 起動時にワールド削除・英語化・自動開始
    // On boot: delete world, reset to English, auto-start
    public static class EventModeAutoStart
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void AutoStartIfEventMode()
        {
            if (!EventExhibitionMode.IsEnabled) return;
            // メインメニュー以外では何もしない
            // Do nothing outside the main menu
            if (SceneManager.GetActiveScene().name != SceneConstant.MainMenuSceneName) return;

            DeleteDefaultWorldDirectory();
            if (!Localize.TrySetLanguage(Localize.DefaultLanguageCode)) Debug.LogError($"EventModeAutoStart: failed to set language to {Localize.DefaultLanguageCode}");
            CreateIdleQuitWatcher();
            LocalGameLauncher.StartLocalGame();

            #region Internal

            void DeleteDefaultWorldDirectory()
            {
                // 新規生成（PlayerPrefs維持）
                // Regenerate world; PlayerPrefs kept
                var worldDirectory = new StartServerSettings().WorldDirectory;
                if (Directory.Exists(worldDirectory)) Directory.Delete(worldDirectory, true);
            }

            void CreateIdleQuitWatcher()
            {
                // イベントモード限定の常駐監視オブジェクトをここでだけ生成する（起動フックを1箇所に集約）
                // Spawn the event-mode-only resident watcher only here, keeping a single boot entry point
                var watcherObject = new GameObject(nameof(EventIdleQuitWatcher));
                Object.DontDestroyOnLoad(watcherObject);
                var watcher = watcherObject.AddComponent<EventIdleQuitWatcher>();
                watcher.SetIdleTimeoutSeconds(EventExhibitionMode.IdleTimeoutSeconds);
            }

            #endregion
        }
    }
}
