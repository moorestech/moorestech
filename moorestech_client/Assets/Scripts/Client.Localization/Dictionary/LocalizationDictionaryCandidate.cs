using System.Collections.Generic;

namespace Client.Localization
{
    /// <summary>
    /// 合成途中の可変辞書。公開snapshotと同じ実言語・原文の分離を保つ。
    /// Mutable dictionaries under composition, keeping the same language/source split as the published snapshot.
    /// </summary>
    internal sealed class LocalizationDictionaryCandidate
    {
        public readonly Dictionary<string, Dictionary<string, string>> Languages;
        public readonly Dictionary<string, string> SourceTexts;

        public LocalizationDictionaryCandidate(
            Dictionary<string, Dictionary<string, string>> languages,
            Dictionary<string, string> sourceTexts)
        {
            Languages = languages;
            SourceTexts = sourceTexts;
        }
    }
}
