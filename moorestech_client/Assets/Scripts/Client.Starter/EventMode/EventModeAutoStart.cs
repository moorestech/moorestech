using System.IO;
using Client.Common;
using Client.Localization;
using Server.Boot;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter.EventMode
{
    // イベント出展モード: 起動時にワールドを削除し英語へ戻して自動でローカルゲームを開始する
    // Event exhibition mode: on boot, delete the world, reset to English, and auto-start the local game
    public static class EventModeAutoStart
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void AutoStartIfEventMode()
        {
            if (!EventExhibitionMode.IsEnabled) return;
            // メインメニュー以外からの起動（テスト・Editor別シーン再生）では何もしない
            // Do nothing when booted outside the main menu (tests, editor playback of other scenes)
            if (SceneManager.GetActiveScene().name != SceneConstant.MainMenuSceneName) return;

            DeleteDefaultWorldDirectory();
            Localize.TrySetLanguage(Localize.DefaultLanguageCode);
            LocalGameLauncher.StartLocalGame();
        }

        private static void DeleteDefaultWorldDirectory()
        {
            // 既定ワールドを削除し毎回新規生成にする（ResetAllDataConfirmPopupと同経路。PlayerPrefsは残す）
            // Delete the default world so every boot generates fresh (same path as ResetAllDataConfirmPopup, PlayerPrefs kept)
            var worldDirectory = new StartServerSettings().WorldDirectory;
            if (Directory.Exists(worldDirectory)) Directory.Delete(worldDirectory, true);
        }
    }
}
