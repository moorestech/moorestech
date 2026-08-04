using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Client.Game.Skit.Localization
{
    public interface ISkitLocalizationDictionaryLoader
    {
        UniTask<IReadOnlyDictionary<string, string>> LoadAsync(string languageCode);
    }
}
