using Client.Common;
using Client.Game.InGame.BugReport.Playtest;
using Client.Localization;
using Client.PlaytestReceiver.Gate;
using Cysharp.Threading.Tasks;
using Game.Paths;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter.EventMode
{
    // 自動開始の可否。照合が確定するまでは待ちで、確定して止められていれば断念する
    // Whether the auto start may go: it waits until the launch check settles, and abandons when the settled verdict blocks
    public enum EventModeAutoStartDecision
    {
        WaitForVerdict,
        Start,
        Abandon,
    }

    // 起動時にワールド削除・起動言語の適用・自動開始
    // On boot: delete world, apply the launch language, auto-start
    public static class EventModeAutoStart
    {
        // 起動時照合は受け口への通信を伴う。前例 StandalonePlaytestSmokeBootstrap と同じ180秒で切り、無応答の受け口に出展機を無期限で預けない
        // The launch check talks to the receiver, so it is bounded at the precedent's 180 seconds and an unresponsive receiver never holds the exhibition machine forever
        private const float LaunchVerdictTimeoutSeconds = 180f;

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

            // 出展モードの自動開始には確認へ答える人が居ない。同意・異常終了確認を出さない無人起動として宣言する（D2 裁定・未応答の印は残る）
            // The exhibition auto start has nobody to answer, so it is declared an unattended boot that shows no consent or crash confirmation (D2 adjudication; the unanswered marks remain)
            PlaytestStartGateBypass.DeclareUnattendedProcess("eventModeAutoStart");

            // 新規生成（PlayerPrefs維持）
            // Regenerate world; PlayerPrefs kept
            GameSystemPaths.DeleteDefaultWorldDirectory();
            ApplyLaunchLanguage(settings);
            StartWhenLaunchVerdictSettles();
        }

        // 無人宣言が効くのは漏斗の2段目だけで、1段目の起動時照合は待たないと通らない。確定前に開始すると漏斗がタイトルへ戻し、この起動フックは二度と発火しない
        // The unattended declaration only covers the funnel's second stage; starting before the launch check settles makes the funnel bounce back to the title, and this boot hook never fires again
        private static void StartWhenLaunchVerdictSettles()
        {
            Debug.Log($"EventModeAutoStart: 起動時照合の確定を待ってから自動開始します（上限{LaunchVerdictTimeoutSeconds}秒）");

            // Forgetに吸われた例外も理由付きで残す（前例: StandalonePlaytestSmokeBootstrap）
            // Exceptions swallowed by Forget are recorded with a reason too (precedent: StandalonePlaytestSmokeBootstrap)
            StartWhenLaunchVerdictSettlesAsync().Forget(exception => Debug.LogError($"EventModeAutoStart: 確定待ちが例外で終わったため自動開始しません {exception.GetType()} {exception.Message}"));
        }

        // 待ちは期限付き（前例 StandalonePlaytestSmokeBootstrap.StartWhenPreconditionsHoldAsync と同じ形）。無応答の受け口で無言の居座りにしない
        // The wait is bounded, in the same shape as the StandalonePlaytestSmokeBootstrap precedent, so a silent receiver never turns into a silent stall
        private static async UniTask StartWhenLaunchVerdictSettlesAsync()
        {
            var startedAt = Time.realtimeSinceStartup;
            var decision = DecideAutoStartWithinDeadline(PlaytestLaunchGate.Current.Value, 0f, LaunchVerdictTimeoutSeconds);
            while (decision == EventModeAutoStartDecision.WaitForVerdict)
            {
                await UniTask.Yield();
                decision = DecideAutoStartWithinDeadline(PlaytestLaunchGate.Current.Value, Time.realtimeSinceStartup - startedAt, LaunchVerdictTimeoutSeconds);
            }

            // 照合に通らない・期限まで確定しない出展機は自動開始しない。理由を出したままタイトルに留める（fail-closed）
            // An exhibition machine that fails the check, or never settles before the deadline, does not auto-start; it stays on the title with the reason shown (fail closed)
            if (decision == EventModeAutoStartDecision.Abandon)
            {
                var verdict = PlaytestLaunchGate.Current.Value;
                var reason = DecideAutoStart(verdict) == EventModeAutoStartDecision.WaitForVerdict
                    ? $"起動時照合が{LaunchVerdictTimeoutSeconds}秒以内に確定しなかった"
                    : "起動時照合に通らなかった";
                Debug.LogError($"EventModeAutoStart: {reason}ため自動開始しません status:{verdict.Status} detail:{verdict.Detail}");
                return;
            }

            LocalGameLauncher.StartLocalGame();
        }

        // 照合結果ごとの可否。待ちと断念を分けて持つので、待っているだけの状態を「失敗」と読み違えない
        // The verdict-by-verdict decision; keeping waiting apart from abandoning stops a mere wait from reading as a failure
        public static EventModeAutoStartDecision DecideAutoStart(PlaytestGateResult verdict)
        {
            if (verdict.Status == PlaytestGateStatus.NotEvaluated || verdict.Status == PlaytestGateStatus.Checking) return EventModeAutoStartDecision.WaitForVerdict;
            return verdict.IsBlocked ? EventModeAutoStartDecision.Abandon : EventModeAutoStartDecision.Start;
        }

        // 待ち時間込みの可否。期限を過ぎても確定しなければ待ちを断念へ倒す（fail-closed）
        // The decision including how long it waited; an unsettled verdict past the deadline falls to abandoning rather than waiting on (fail closed)
        public static EventModeAutoStartDecision DecideAutoStartWithinDeadline(PlaytestGateResult verdict, float secondsWaited, float timeoutSeconds)
        {
            var decision = DecideAutoStart(verdict);
            if (decision != EventModeAutoStartDecision.WaitForVerdict) return decision;
            return secondsWaited < timeoutSeconds ? EventModeAutoStartDecision.WaitForVerdict : EventModeAutoStartDecision.Abandon;
        }
    }
}
