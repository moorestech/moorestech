using Client.Localization;
using UnityEngine;

namespace Client.Starter.Localization
{
    // 起動時のEditor門番は言語解決の外に置く
    // Keep the editor gate outside language resolution at boot
    public static class SteamStartupLanguage
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        internal static void ApplyAtBoot()
        {
            if (Application.isEditor)
            {
                Debug.Log($"[SteamStartupLanguage] staying on {Localize.GetCurrentLanguageCode()}: editor session does not consult Steam");
                return;
            }

            // 選択済み判定はLocalizeに任せ、Steam読み取りを避ける
            // Let Localize check the saved choice before it reads Steam
            Localize.TryApplyUnchosenLanguage(new SteamUnchosenLanguageSource());
        }
    }
}
