using System;
using Client.Common;
using Client.Game.Common;
using Client.PlaytestReceiver.Gate;
using Cysharp.Threading.Tasks;
using Game.Paths;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter.PlaytestSmoke
{
    /// <summary>
    /// 配布ビルドを引数で自動運転する。メインメニューを人手で押さずローカルゲームを開始する
    /// Drives a distribution build from arguments, starting the local game without a human pressing the menu
    /// 前例は Client.Starter/StandaloneQa/StandaloneTerrainQaBootstrap（同じ引数マーカー＋result.json＋Quitの型）
    /// The precedent is StandaloneTerrainQaBootstrap: the same marker-argument, result.json and Quit shape
    /// </summary>
    public static class StandalonePlaytestSmokeBootstrap
    {
        // 起動時照合は受け口への通信を伴う。応答が無いまま検証機を占有し続けないよう期限を切る
        // The launch check talks to the receiver; bound it so a silent receiver never holds the verifier forever
        private const float LaunchGateTimeoutSeconds = 180f;

        // 初期化完了の期限は前例 StandaloneTerrainQaBootstrap と同じ120秒
        // The initialization deadline matches the 120 seconds of the StandaloneTerrainQaBootstrap precedent
        private const float GameInitializationTimeoutSeconds = 120f;

        public static bool IsActive { get; private set; }
        public static StandalonePlaytestSmokeSettings Settings { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void AutoStartIfSmokeRun()
        {
            // Editorは開発者のワールドを不可逆に消すため、配布ビルドでのみ動かす
            // The Editor would irreversibly wipe a developer's world, so this runs only in a player build
            if (Application.isEditor) return;

            var args = Environment.GetCommandLineArgs();
            if (!StandalonePlaytestSmokeSettings.HasMarker(args)) return;
            if (!StandalonePlaytestSmokeSettings.TryParse(args, out var settings, out var error))
            {
                Debug.LogError($"[PlaytestSmoke] {error}");
                Application.Quit(2);
                return;
            }

            // メインメニュー以外（初期化中・ゲーム中）で二重に開始しない
            // Never start twice from anywhere but the main menu
            var activeSceneName = SceneManager.GetActiveScene().name;
            if (activeSceneName != SceneConstant.MainMenuSceneName)
            {
                Debug.LogError($"[PlaytestSmoke] not started: the boot scene was {activeSceneName}, not {SceneConstant.MainMenuSceneName}");
                Application.Quit(2);
                return;
            }

            Settings = settings;
            IsActive = true;

            // Forgetに吸われた例外（ワールド削除のIO失敗等）も理由付きの失敗結果にする（前例: InitializeScenePipeline）
            // Exceptions swallowed by Forget, such as world deletion IO failures, also become a reasoned failure (precedent: InitializeScenePipeline)
            StartWhenPreconditionsHoldAsync(settings).Forget(exception => FailBeforeRun(settings, "bootstrap", $"{exception.GetType()} {exception.Message}"));
        }

        private static async UniTask StartWhenPreconditionsHoldAsync(StandalonePlaytestSmokeSettings settings)
        {
            // 照合の結論を待つ。判定前に開始すると初期化パイプラインがメニューへ戻し、無人のまま止まる
            // Wait for the launch verdict; starting before it makes the pipeline bounce back to the menu and stall unattended
            var gateDeadline = Time.realtimeSinceStartup + LaunchGateTimeoutSeconds;
            while (IsGatePending(PlaytestLaunchGate.Current.Value.Status) && Time.realtimeSinceStartup < gateDeadline)
            {
                await UniTask.Yield();
            }

            var failure = StandalonePlaytestSmokePreconditions.FindFailure(settings, PlaytestLaunchGate.Current.Value);
            if (failure.Length != 0)
            {
                FailBeforeRun(settings, "preconditions", failure);
                return;
            }

            // phase1だけ新規ワールドから始める。phase2は直前のphase1が残したワールドをロードする
            // Only phase1 starts from a fresh world; phase2 loads the world phase1 left behind
            if (settings.Phase == StandalonePlaytestSmokeSettings.PhaseOne)
                GameSystemPaths.DeleteDefaultWorldDirectory();

            // 初期化完了とメインメニューへの差し戻しを、開始より先に購読しておく（前例と同じ購読＋期限付き待機）
            // Subscribe to initialization completion and a bounce back to the main menu before starting (same subscribe-plus-deadline shape as the precedent)
            var gameInitialized = false;
            var returnedToMainMenu = false;
            using var initializedSubscription = GameInitializedEvent.OnGameInitialized.Take(1).Subscribe(_ => gameInitialized = true);
            SceneManager.sceneLoaded += OnSceneLoaded;

            Debug.Log($"[PlaytestSmoke] starting {settings.Phase} result:{settings.ResultDirectory}");
            LocalGameLauncher.StartLocalGame();

            var initializationDeadline = Time.realtimeSinceStartup + GameInitializationTimeoutSeconds;
            while (!gameInitialized && !returnedToMainMenu && Time.realtimeSinceStartup < initializationDeadline)
            {
                await UniTask.Yield();
            }
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (gameInitialized) return;

            // 初期化例外はメニューへ戻し、ブートストラップは再実行されない。無応答の開始ゲートは期限で拾う
            // An initialization exception returns to the menu where the bootstrap never reruns; an unanswered start gate is caught by the deadline
            FailBeforeRun(settings, "game-initialized", returnedToMainMenu
                ? "game initialization failed and returned to the main menu; see the preceding error log"
                : $"game initialization did not complete within {GameInitializationTimeoutSeconds}s (a start gate such as the previous-crash confirmation may be waiting for input)");

            #region Internal

            bool IsGatePending(PlaytestGateStatus status)
            {
                return status == PlaytestGateStatus.NotEvaluated || status == PlaytestGateStatus.Checking;
            }

            void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.name == SceneConstant.MainMenuSceneName) returnedToMainMenu = true;
            }

            #endregion
        }

        // 通し手順に入る前の失敗。以降の初期化完了で手順が走らないよう無効化し、理由を結果とログへ残して終了する
        // A failure before the steps run; deactivate so a later initialization never runs them, record the reason and quit
        private static void FailBeforeRun(StandalonePlaytestSmokeSettings settings, string stepName, string reason)
        {
            IsActive = false;
            Debug.LogError($"[PlaytestSmoke] {settings.Phase} failed at {stepName}: {reason}");
            StandalonePlaytestSmokeResultWriter.Write(settings.ResultDirectory, StandalonePlaytestSmokeResultWriter.CreateSingleStepFailure(settings.Phase, stepName, reason));
            Application.Quit(1);
        }
    }
}
