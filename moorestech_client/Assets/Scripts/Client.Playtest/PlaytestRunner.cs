using System;
using Client.Playtest.Core;
using Client.Playtest.Overlay;
using Client.Playtest.Recording;
using Client.Playtest.WebUi;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest
{
    /// <summary>
    ///     シナリオを1回のexecute-dynamic-code呼び出しで受け付け、完走後にresult.jsonを書き出すランナー
    ///     Accepts a scenario from a single execute-dynamic-code call and writes result.json when it finishes
    /// </summary>
    public static class PlaytestRunner
    {
        private static bool _isRunning;

        public static string Run(string runName, PlaytestRunOptions options, Func<PlaytestDriver, UniTask> scenario)
        {
            // 実行前ガード（PlayMode中のみ・多重実行禁止）
            // Pre-run guards (play mode only, no concurrent runs)
            if (!Application.isPlaying) return "ERROR: not in play mode";
            if (_isRunning) return "ERROR: another scenario is running";

            var runDirectory = PlaytestPaths.CreateRunDirectory(runName);
            RunAsync().Forget();
            return runDirectory;

            #region Internal

            async UniTask RunAsync()
            {
                _isRunning = true;
                var result = new PlaytestResult { RunName = runName, StartedAt = DateTime.Now.ToString("O") };
                var logCollector = new PlaytestLogCollector();
                logCollector.StartCollect();

                // ゲーム初期化を待ってから録画開始・シナリオ実行
                // Wait for game initialization, then start recording and run the scenario
                PlaytestRecorder recorder = null;
                var driver = new PlaytestDriver(result, runDirectory);
                try
                {
                    await PlaytestGameReady.WaitUntilReady(options.ReadyTimeoutSeconds);

                    // Game Viewが非フォーカスだと注入キーが全て捨てられるため、フォーカスは動かさず入力ルーティング設定だけ退避・上書きする
                    // Injected keys are discarded while the Game View is unfocused, so override only the input routing settings and never move focus
                    PlaytestInputFocusOverride.Enable();

                    // DOM応答ハンドラを登録し、CEF利用時だけInputSystem転送を実行中に有効化する
                    // Register the DOM response handler and enable InputSystem forwarding only while CEF is in use
                    PlaytestDomQuery.RegisterHandler();
                    if (CefScreenMapper.IsWebUiAvailable()) CefInputForwarder.StartForwarding();

                    // 録画に焼き込むオーバーレイを初期化し、開始ナレーションを流す
                    // Initialize the overlay baked into the recording and post the opening narration
                    PlaytestOverlay.EnsureCreatedAndReset();
                    PlaytestOverlay.PushNote($"シナリオ開始: {runName}");
                    if (options.Record) recorder = PlaytestRecorder.StartRecording(runDirectory);
                    await scenario(driver).Timeout(TimeSpan.FromSeconds(options.ScenarioTimeoutSeconds));
                    result.Success = result.Asserts.TrueForAll(assert => assert.Passed);
                }
                catch (Exception exception)
                {
                    // シナリオの失敗もresult.jsonへ落とすため、ここでのみ例外を捕捉する
                    // Catch exceptions only here so scenario failures still land in result.json
                    result.Success = false;
                    result.Error = $"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}";
                }

                // 成否にかかわらず転送を止め、入力ルーティング設定を元へ戻し、次回実行へDOM応答を持ち越さない
                // Stop forwarding, restore the input routing settings and discard pending DOM responses regardless of success or failure
                PlaytestInputFocusOverride.Restore();
                CefInputForwarder.StopForwarding();
                PlaytestDomQuery.ResetPending();

                // 後始末して結果を書き出す（result.jsonの出現が完走シグナル）
                // Clean up and write the result (result.json's appearance signals completion)
                recorder?.StopRecording(result);
                logCollector.StopCollect();
                result.ErrorLogs.AddRange(logCollector.ErrorLogs);
                result.FinishedAt = DateTime.Now.ToString("O");
                result.Write(runDirectory);
                _isRunning = false;
            }

            #endregion
        }
    }
}
