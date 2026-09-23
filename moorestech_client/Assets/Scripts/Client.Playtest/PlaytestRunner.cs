using System;
using Client.Playtest.Core;
using Client.Playtest.Input;
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
                    try
                    {
                    using var inputScope = new PlaytestInputScope();
                    await PlaytestGameReady.WaitUntilReady(options.ReadyTimeoutSeconds);

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
                    finally
                    {
                        try { CefInputForwarder.StopForwarding(); }
                        finally
                        {
                            try { PlaytestDomQuery.ResetPending(); }
                            finally
                            {
                                try { recorder?.StopRecording(result); }
                                finally { logCollector.StopCollect(); }
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    // シナリオの失敗もresult.jsonへ落とすため、ここでのみ例外を捕捉する
                    // Catch exceptions only here so scenario failures still land in result.json
                    result.Success = false;
                    result.Error = $"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}";
                }

                // 入力と録画の復元が終わってから完走を公開する。
                // Publish completion only after input and recording cleanup have finished.
                result.ErrorLogs.AddRange(logCollector.ErrorLogs);
                result.FinishedAt = DateTime.Now.ToString("O");
                _isRunning = false;
                result.Write(runDirectory);
            }

            #endregion
        }
    }
}
