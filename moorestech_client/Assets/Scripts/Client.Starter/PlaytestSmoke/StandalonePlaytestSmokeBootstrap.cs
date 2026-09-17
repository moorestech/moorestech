using System;
using System.IO;
using Client.Common;
using Client.Game.InGame.BugReport.Playtest;
using Client.PlaytestReceiver.Gate;
using Cysharp.Threading.Tasks;
using Game.Paths;
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
            StartWhenPreconditionsHoldAsync(settings).Forget();
        }

        private static async UniTask StartWhenPreconditionsHoldAsync(StandalonePlaytestSmokeSettings settings)
        {
            // 照合の結論を待つ。判定前に開始すると初期化パイプラインがメニューへ戻し、無人のまま止まる
            // Wait for the launch verdict; starting before it makes the pipeline bounce back to the menu and stall unattended
            var deadline = Time.realtimeSinceStartup + LaunchGateTimeoutSeconds;
            while (IsGatePending(PlaytestLaunchGate.Current.Value.Status) && Time.realtimeSinceStartup < deadline)
            {
                await UniTask.Yield();
            }

            var failure = FindPreconditionFailure(settings, PlaytestLaunchGate.Current.Value);
            if (failure.Length != 0)
            {
                Debug.LogError($"[PlaytestSmoke] {settings.Phase} not started: {failure}");
                StandalonePlaytestSmokeResultWriter.Write(settings.ResultDirectory, StandalonePlaytestSmokeResultWriter.CreateSingleStepFailure(settings.Phase, "preconditions", failure));
                Application.Quit(1);
                return;
            }

            // phase1だけ新規ワールドから始める。phase2は直前のphase1が残したワールドをロードする
            // Only phase1 starts from a fresh world; phase2 loads the world phase1 left behind
            if (settings.Phase == StandalonePlaytestSmokeSettings.PhaseOne)
                GameSystemPaths.DeleteDefaultWorldDirectory();

            Debug.Log($"[PlaytestSmoke] starting {settings.Phase} result:{settings.ResultDirectory}");
            LocalGameLauncher.StartLocalGame();

            #region Internal

            bool IsGatePending(PlaytestGateStatus status)
            {
                return status == PlaytestGateStatus.NotEvaluated || status == PlaytestGateStatus.Checking;
            }

            #endregion
        }

        // 無人では越えられない関門を開始前に検出し、止まる代わりに理由付きで失敗させる
        // Detects gates nobody can pass unattended before starting, failing with a reason instead of stalling
        private static string FindPreconditionFailure(StandalonePlaytestSmokeSettings settings, PlaytestGateResult gate)
        {
            // 通し検証の対象は照合を通った配布版だけ。開発者モードでは報告が受け口へ運ばれない
            // Only a checked distribution build is in scope; in developer mode no report ever reaches the receiver
            if (gate.Status != PlaytestGateStatus.Allowed)
                return $"launch gate is {gate.Status} (Allowed required; developer mode means build-info.json is missing or Steam is not running) {gate.Detail}";

            // 同意表示は応答を上限なく待つ。検証機では初回セットアップで一度だけ人が既読にする
            // The consent notice waits without bound; on the verifier a human acknowledges it once during setup
            if (!PlaytestConsentFlag.IsAcknowledged())
                return $"the playtest consent notice has not been acknowledged on this machine ({PlaytestConsentFlag.FilePath}); acknowledge it once interactively";

            // phase2はphase1のセーブを読む検証。ワールドが無いと新規生成され、ロードを確かめないまま進んでしまう
            // phase2 verifies loading phase1's save; without the world a fresh one is generated and loading goes unverified
            if (settings.Phase == StandalonePlaytestSmokeSettings.PhaseTwo && !Directory.Exists(GameSystemPaths.DefaultWorldDirectory))
                return $"no saved world to load at {GameSystemPaths.DefaultWorldDirectory}; run phase1 first";

            return "";
        }
    }
}
