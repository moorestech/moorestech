using Client.Localization;
using UnityEngine;

namespace Client.Starter.Localization
{
    // 未選択のプレイヤーに起動時だけSteam言語を当てる
    // Apply Steam language at startup only for players without a choice
    public static class SteamStartupLanguage
    {
        // 最初のシーンのAwakeでSteamが初期化された後に読む
        // Read after the first scene's Awake initializes Steam
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyAtBoot()
        {
            Apply(new SteamGameLanguageReader());
        }

        internal static void Apply(ISteamGameLanguageReader reader)
        {
            // 選択済みは保存値優先で戻る
            // Prefer the saved choice without consulting Steam
            if (Localize.HasChosenLanguage()) return;

            // 取得失敗は理由を記録して英語のまま進む
            // Log why the read failed and continue in English
            if (!reader.TryRead(out var steamLanguage, out var failureReason))
            {
                Debug.Log($"[SteamStartupLanguage] staying on {Localize.GetCurrentLanguageCode()}: {failureReason}");
                return;
            }

            // 対応無しは理由記録し現状維持
            // Log why the mapping failed and keep the current language when unmapped
            if (!SteamLanguageMapping.TryToGameLanguage(steamLanguage, out var gameLanguage))
            {
                Debug.Log($"[SteamStartupLanguage] staying on {Localize.GetCurrentLanguageCode()}: Steam language {steamLanguage} has no mapping in localization_settings.csv");
                return;
            }

            if (!Localize.TryApplyUnchosenLanguage(gameLanguage))
                Debug.LogWarning($"[SteamStartupLanguage] could not apply {gameLanguage} for Steam language {steamLanguage}");
        }
    }
}
