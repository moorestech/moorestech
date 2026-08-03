using System.Collections.Generic;

namespace Client.Localization
{
    internal sealed class PublishedLocalizationDictionarySnapshot
    {
        public readonly long Revision;

        // 選択可能な実言語と原文を別フィールドに持ち、除外規則を型で不要にする
        // Selectable languages and source texts live in separate fields so no exclusion rule is needed
        public readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Languages;
        public readonly IReadOnlyDictionary<string, string> SourceTexts;

        public PublishedLocalizationDictionarySnapshot(
            long revision,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> languages,
            IReadOnlyDictionary<string, string> sourceTexts)
        {
            Revision = revision;
            Languages = languages;
            SourceTexts = sourceTexts;
        }
    }
}
