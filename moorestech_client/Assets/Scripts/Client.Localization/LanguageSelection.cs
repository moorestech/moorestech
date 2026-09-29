using System.Collections.Generic;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Localization
{
    // 一覧のindexと言語コードの対応をここだけで持つ
    // Hold the index-to-code correspondence of the list in one place
    public static class LanguageSelection
    {
        public static List<string> GetDisplayNames()
        {
            var displayNames = new List<string>();
            foreach (var language in LanguageCatalog.Languages)
                displayNames.Add(language.DisplayName);
            return displayNames;
        }

        // 現在言語が一覧に無いのは生成物と実言語の不整合なので黙って-1を返さない
        // A current language absent from the list means catalog drift, so never return -1 silently
        public static int GetCurrentIndex()
        {
            var currentLanguageCode = Localize.GetCurrentLanguageCode();
            for (var index = 0; index < LanguageCatalog.Languages.Length; index++)
                if (LanguageCatalog.Languages[index].Code == currentLanguageCode)
                    return index;

            Debug.LogError($"[LanguageSelection] current language {currentLanguageCode} is absent from the language catalog");
            return -1;
        }

        public static bool TrySetByIndex(int index)
        {
            if (index < 0 || index >= LanguageCatalog.Languages.Length)
            {
                Debug.LogWarning($"[LanguageSelection] language rejected: index {index} is outside the language catalog");
                return false;
            }

            return Localize.TrySetChosenLanguage(LanguageCatalog.Languages[index].Code);
        }
    }
}
