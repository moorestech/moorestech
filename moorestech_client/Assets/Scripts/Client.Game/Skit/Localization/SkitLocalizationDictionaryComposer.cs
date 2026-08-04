using System.Collections.Generic;
using Client.Localization;

namespace Client.Game.Skit.Localization
{
    internal sealed class SkitLocalizationDictionaryComposer
    {
        private readonly ISkitLocalizationSource _source;

        public SkitLocalizationDictionaryComposer(ISkitLocalizationSource source)
        {
            _source = source;
        }

        public SkitLocalizationScope Compose(
            string targetLanguageCode,
            IReadOnlyDictionary<string, string> targetSkit,
            IReadOnlyDictionary<string, string> englishSkit)
        {
            // mod辞書を優先し、欠落した値だけをSkit辞書から補う
            // Prioritize mod dictionaries and fill only missing values from Skit dictionaries
            var target = CopyModDictionary(targetLanguageCode);
            var english = CopyModDictionary(Localize.DefaultLanguageCode);
            AddMissingNonEmpty(target, targetSkit);
            AddMissingNonEmpty(english, englishSkit);
            return new SkitLocalizationScope(target, english);

            #region Internal

            Dictionary<string, string> CopyModDictionary(string languageCode)
            {
                var result = new Dictionary<string, string>();
                if (!_source.TryGetDictionary(languageCode, out var dictionary)) return result;
                // 空文字は下位辞書へフォールバックできるよう欠落として扱う
                // Treat empty values as missing so lower-priority dictionaries can provide them
                foreach (var pair in dictionary)
                {
                    if (!string.IsNullOrEmpty(pair.Value))
                    {
                        result.Add(pair.Key, pair.Value);
                    }
                }
                return result;
            }

            void AddMissingNonEmpty(
                Dictionary<string, string> destination,
                IReadOnlyDictionary<string, string> skitDictionary)
            {
                // 既存のmod値を上書きせず有効なSkit値だけを追加する
                // Add only valid Skit values without overwriting existing mod values
                foreach (var pair in skitDictionary)
                {
                    if (!string.IsNullOrEmpty(pair.Value) && !destination.ContainsKey(pair.Key))
                    {
                        destination.Add(pair.Key, pair.Value);
                    }
                }
            }

            #endregion
        }
    }
}
