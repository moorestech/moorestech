using System;
using System.Collections.Generic;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;
using UniRx;

namespace Client.Game.Skit.Localization
{
    public sealed class LocalizeSkitLocalizationSource : ISkitLocalizationSource
    {
        public string GetCurrentLanguageCode()
        {
            return Localize.GetCurrentLanguageCode();
        }

        public IObservable<Unit> GetLanguageChanged()
        {
            return Localize.OnLanguageChanged;
        }

        public bool TryGetDictionary(
            string languageCode,
            out IReadOnlyDictionary<string, string> dictionary)
        {
            return Localize.TryGetDictionary(languageCode, out dictionary);
        }

        public SkitCharacterLocalizationIdentity GetCharacterIdentity(string characterId)
        {
            var character = MasterHolder.CharacterMaster.GetCharacterMaster(characterId);
            return new SkitCharacterLocalizationIdentity(
                ContentLocalizationKeys.CharacterName(character.CharacterGuid).Key,
                character.DisplayName);
        }
    }
}
