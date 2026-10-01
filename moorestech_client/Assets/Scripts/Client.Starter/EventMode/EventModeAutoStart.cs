using System;
using Client.Common;
using Client.Game.InGame.BugReport.Playtest;
using Client.Localization;
using Client.Starter.Playtest.TitleGates;
using Cysharp.Threading.Tasks;
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

            // 起動言語はメインメニューが出る瞬間から効くよう、待ちより先に同期で適用する
            // The launch language applies synchronously before the wait so it is in effect from the moment the main menu shows
            ApplyLaunchLanguage(settings);

            // Forgetに吸われた例外も理由付きで残す（前例: StandalonePlaytestSmokeBootstrap）
            // Exceptions swallowed by Forget are recorded with a reason too (precedent: StandalonePlaytestSmokeBootstrap)
            StartWhenTitleGatesPassAsync().Forget(LogAutoStartException);

            #region Internal

            // 確認列はタイトル合成ルートのStartで始まり、AfterSceneLoadのここより遅い。待たずに開始すると初期化が「確認が未開始」で断りメニューへ戻す
            // The title sequence starts in the title composition root's Start, later than this AfterSceneLoad hook; starting without waiting is refused as "not started" and bounced to the menu
            async UniTask StartWhenTitleGatesPassAsync()
            {
                // 期限切れは起動失敗。メニューに残すと前の来場者のワールドで遊べ、ブースも止まるので、ワールドを消さずに終了する
                // Expiry is a failed boot; staying on the menu would let a visitor resume the previous world and stall the booth, so quit without wiping
                if (!await PlaytestTitleGates.WaitUntilPassedWithinDeadlineAsync(PlaytestTitleGates.UnattendedPassTimeoutSeconds, Application.exitCancellationToken))
                {
                    Debug.LogError($"EventModeAutoStart: title gates did not pass within {PlaytestTitleGates.UnattendedPassTimeoutSeconds}s (the title composition root may not have started the sequence); quitting without wiping the world so the loop script restarts the game");
                    // 出展のループスクリプトが終了を検知して再起動する前提（Editorでは無効）
                    // Relies on the exhibition loop script restarting the game on exit (no-op in the Editor)
                    Application.Quit();
                    return;
                }

                // 開始が確定してから消す。断られた時にワールドだけ消える状態を作らない
                // Wipe only once the start is settled, so a refusal never leaves the world deleted with no game started
                GameSystemPaths.DeleteDefaultWorldDirectory();
                LocalGameLauncher.StartLocalGame();
            }

            // 終了によるキャンセルも含め、自動開始しなかった理由を必ずログへ出す
            // Always log why the auto start did not happen, cancellation by application exit included
            void LogAutoStartException(Exception exception)
            {
                if (exception is OperationCanceledException)
                {
                    Debug.Log("EventModeAutoStart: the wait for the title gates was cancelled by application exit; not auto-starting");
                    return;
                }
                Debug.LogError($"EventModeAutoStart: the auto start ended with an exception; not auto-starting {exception.GetType()} {exception.Message}");
            }

            #endregion
        }
    }
}
