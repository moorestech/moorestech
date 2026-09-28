using Client.Common;
using Client.Game.Common;
using Client.Network.API.Identity;
using Client.Starter.Initialization.Progress;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter.Initialization.Refusal
{
    internal static class InitializationFailurePresenter
    {
        internal static async UniTask ShowRefusalAsync(PlayerStartRefusal refusal, LoadingProgressLog progressLog)
        {
            Debug.LogWarning(refusal.LogReason);
            await ShowFailureAsync(refusal.Key, progressLog);
        }

        internal static async UniTask ShowInitializationFailedAsync(LoadingProgressLog progressLog)
        {
            await ShowFailureAsync(LocalizationKeys.Ui.Loading.InitializationFailed, progressLog);
        }

        private static async UniTask ShowFailureAsync(LocalizationKey key, LoadingProgressLog progressLog)
        {
            // 拒否と初期化失敗を同じ終了口へ流し、表示後にメニューへ戻す
            // Route refusals and failures through one shutdown path, then return to the menu
            GameShutdownEvent.FireGameShutdown(GameShutdownReason.InitializationFailed);
            progressLog.Append(key);
            await UniTask.Delay(2000);
            SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
        }
    }
}
