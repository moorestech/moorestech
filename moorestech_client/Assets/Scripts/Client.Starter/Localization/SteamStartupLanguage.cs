using Client.Localization;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.Localization
{
    // Steam初期化が遅れても当てる
    // Land the language even when Steam initializes late
    internal static class SteamStartupLanguage
    {
        private const int MaxAttemptCount = 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        internal static void ApplyAtBoot()
        {
            ApplyUntilSteamAnswers().Forget();
        }

        // 選択済み判定はLocalizeに任せる
        // Let Localize check the saved choice
        internal static bool TryApplyOnce()
        {
            return Localize.TryApplyUnchosenLanguage(new SteamUnchosenLanguageSource(new SteamGameLanguageReader()));
        }

        private static async UniTaskVoid ApplyUntilSteamAnswers()
        {
            for (var attempt = 1; attempt <= MaxAttemptCount; attempt++)
            {
                if (TryApplyOnce()) return;
                if (attempt < MaxAttemptCount) await UniTask.NextFrame();
            }
        }
    }
}
