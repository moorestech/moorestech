using System;
using System.Collections.Generic;
using UniRx;

namespace Client.Game.Skit.Localization
{
    public interface ISkitLocalizationSource
    {
        string GetCurrentLanguageCode();
        IObservable<Unit> GetLanguageChanged();

        bool TryGetDictionary(
            string languageCode,
            out IReadOnlyDictionary<string, string> dictionary);

        SkitCharacterLocalizationIdentity GetCharacterIdentity(string characterId);
    }

    public readonly struct SkitCharacterLocalizationIdentity
    {
        public readonly string Key;
        public readonly string SourceText;

        public SkitCharacterLocalizationIdentity(string key, string sourceText)
        {
            Key = key;
            SourceText = sourceText;
        }
    }
}
