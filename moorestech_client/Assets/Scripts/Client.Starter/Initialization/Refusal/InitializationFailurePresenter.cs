using System.Threading;
using Client.Common;
using Client.Game.Common;
using Client.Starter.Initialization.Progress;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter.Initialization.Refusal
{
    internal static class InitializationFailurePresenter
    {
        internal static async UniTask ShowRefusalAsync(PlayerStartRefusal refusal, LoadingProgressLog progressLog, CancellationToken exitToken)
        {
            Debug.LogWarning(refusal.LogReason);
            await ShowFailureAsync(refusal.Key, progressLog, exitToken);
        }

        internal static async UniTask ShowInitializationFailedAsync(LoadingProgressLog progressLog, CancellationToken exitToken)
        {
            await ShowFailureAsync(LocalizationKeys.Ui.Loading.InitializationFailed, progressLog, exitToken);
        }

        private static async UniTask ShowFailureAsync(LocalizationKey key, LoadingProgressLog progressLog, CancellationToken exitToken)
        {
            // 拒否と初期化失敗を同じ終了口へ流し、表示後にメニューへ戻す
            // Route refusals and failures through one shutdown path, then return to the menu
            GameShutdownEvent.FireGameShutdown(GameShutdownReason.InitializationFailed);
            progressLog.Append(key);
            await UniTask.Delay(2000, cancellationToken: exitToken);

            // Play終了後に再開した継続がシーンロードで編集中シーンを壊さないよう、直前で止める
            // Stop right before the scene load so a continuation resumed after play-mode exit never clobbers the edited scene
            exitToken.ThrowIfCancellationRequested();
            SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
        }
    }
}
