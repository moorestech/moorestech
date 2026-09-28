using Client.Common;
using Client.Game.InGame.BugReport.Playtest;
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

            // 未指定は言語に触らない
            // Unset touches no language
            if (string.IsNullOrEmpty(requestedLanguageCode)) return;

            // 可否判定は公開辞書だけに任せる
            // Acceptance is decided by the published dictionary alone
            if (Localize.TrySetChosenLanguage(requestedLanguageCode)) return;

            Debug.LogError($"EventModeAutoStart: unknown {EventExhibitionSettings.LanguageEnvKey}={requestedLanguageCode}, keeping {Localize.GetCurrentLanguageCode()}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void AutoStartIfEventMode()
        {
            var settings = EventExhibitionSettings.FromEnvironment();
            if (!ShouldRun(settings, SceneManager.GetActiveScene().name)) return;

            // 出展モードの自動開始には確認へ答える人が居ない。同意・異常終了確認を出さない無人起動として宣言する（D2 裁定・未応答の印は残る）
            // The exhibition auto start has nobody to answer, so it is declared an unattended boot that shows no consent or crash confirmation (D2 adjudication; the unanswered marks remain)
            PlaytestStartGateBypass.DeclareUnattendedProcess("eventModeAutoStart");

            // 起動言語を適用してから、新規生成して同期で開始する（ADR 0070）
            // Apply the launch language, then regenerate the world and start synchronously (ADR 0070)
            ApplyLaunchLanguage(settings);
            GameSystemPaths.DeleteDefaultWorldDirectory();
            LocalGameLauncher.StartLocalGame();
        }
    }
}
