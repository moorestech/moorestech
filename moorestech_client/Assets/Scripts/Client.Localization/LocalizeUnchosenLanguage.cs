using System.Collections.Generic;
using UnityEngine;

namespace Client.Localization
{
    internal static class LocalizeUnchosenLanguage
    {
        public static bool HasChosenLanguage(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> languages)
        {
            if (!PlayerPrefs.HasKey(Localize.LanguagePreferenceKey)) return false;
            return languages.ContainsKey(PlayerPrefs.GetString(Localize.LanguagePreferenceKey));
        }

        public static bool TryApply(
            string languageCode,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> languages)
        {
            // 選択済みまたは不正な言語は理由を記録して拒否する
            // Reject chosen or invalid languages with a logged reason
            if (HasChosenLanguage(languages))
            {
                Debug.Log("[Localize] temporary language rejected: the player already chose a language");
                return false;
            }

            if (string.IsNullOrEmpty(languageCode))
            {
                Debug.LogWarning("[Localize] temporary language rejected: language code is empty");
                return false;
            }

            if (!languages.ContainsKey(languageCode))
            {
                Debug.LogWarning($"[Localize] temporary language rejected: unsupported code {languageCode}");
                return false;
            }

            Localize.SetCurrentLanguageWithoutPersisting(languageCode);
            return true;
        }
    }
}
