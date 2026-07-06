using System;
using System.Threading;
using Client.Common;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter.Initialization
{
    /// <summary>
    /// メインゲームシーンを非アクティブ状態でロードし、アクティブ化待ちの AsyncOperation を返す
    /// Loads the main game scene without activation and returns the AsyncOperation awaiting activation
    /// </summary>
    public class MainGameSceneLoader
    {
        private readonly TMP_Text _loadingLog;
        private readonly System.Diagnostics.Stopwatch _loadingStopwatch;

        public MainGameSceneLoader(TMP_Text loadingLog, System.Diagnostics.Stopwatch loadingStopwatch)
        {
            _loadingLog = loadingLog;
            _loadingStopwatch = loadingStopwatch;
        }

        public async UniTask<AsyncOperation> RunAsync()
        {
            var sceneLoadTask = SceneManager.LoadSceneAsync(SceneConstant.MainGameSceneName, LoadSceneMode.Single);
            sceneLoadTask.allowSceneActivation = false;

            var sceneLoadCts = new CancellationTokenSource();

            // シーンロード完了は 0.9f 到達を CTS キャンセルで検知する（allowSceneActivation=false のため）
            // Scene-load completion is detected via CTS cancel when reaching 0.9f (allowSceneActivation is false)
            try
            {
                await sceneLoadTask.ToUniTask(Progress.Create<float>(
                        x =>
                        {
                            if (x < 0.9f) return;
                            sceneLoadCts.Cancel(); //シーンの読み込みが完了したら終了 allowSceneActivationがfalseの時は0.9fで止まる
                        })
                    , cancellationToken: sceneLoadCts.Token);
            }
            catch (OperationCanceledException)
            {
                // シーンロード完了
                // Scene load complete.
            }

            _loadingLog.text += $"\nシーンロード完了  {_loadingStopwatch.Elapsed}";

            return sceneLoadTask;
        }
    }
}
