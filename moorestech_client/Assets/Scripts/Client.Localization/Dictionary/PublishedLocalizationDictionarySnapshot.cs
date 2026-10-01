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

        public bool TryGetDictionary(
            string languageCode,
            long expectedRevision,
            out IReadOnlyDictionary<string, string> dictionary)
        {
            // revisionと辞書を同じsnapshotから検証し、HTTP応答の異世代混在を防ぐ
            // Validate revision and dictionary from one snapshot to prevent mixed HTTP generations
            if (Revision == expectedRevision && Languages.TryGetValue(languageCode, out var values))
            {
                dictionary = values;
                return true;
            }

            dictionary = null;
            return false;
        }

        public bool TryGetSourceTexts(
            long expectedRevision,
            out IReadOnlyDictionary<string, string> sourceTexts)
        {
            // 原文も同じsnapshotでrevisionを検証し、実言語と同じ世代保証で配信する
            // Source texts validate the revision on the same snapshot for the same generation guarantee
            if (Revision == expectedRevision)
            {
                sourceTexts = SourceTexts;
                return true;
            }

            sourceTexts = null;
            return false;
        }
    }
}
