using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Client.Common.Asset;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Game.Skit.Localization
{
    public sealed class SkitLocalizationDictionaryLoader : ISkitLocalizationDictionaryLoader
    {
        public async UniTask<IReadOnlyDictionary<string, string>> LoadAsync(string languageCode)
        {
            var address = $"Vanilla/Skit/i18n/{languageCode}";
            var textAsset = await AddressableLoader.LoadAsyncDefault<TextAsset>(address);
            if (textAsset == null)
            {
                throw new InvalidOperationException(
                    $"Skit localization asset could not be loaded: {address}");
            }

            return Parse(address, textAsset.text);
        }

        public static IReadOnlyDictionary<string, string> Parse(string address, string json)
        {
            JObject root;
            try
            {
                // 外部入力JSONの構文エラーをaddress付きエラーへ隔離する
                // Isolate external JSON syntax errors and surface the source address
                root = JObject.Parse(
                    json,
                    new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    });
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    $"Skit localization JSON is invalid: {address}",
                    exception);
            }

            if (root["translations"] is not JObject translations)
            {
                throw new InvalidOperationException(
                    $"Skit localization translations are missing: {address}");
            }

            // runtime用skitキーだけを非空値で公開する
            // Publish only non-empty runtime skit keys
            var result = new Dictionary<string, string>();
            foreach (var property in translations.Properties())
            {
                var value = property.Value.Type == JTokenType.String
                    ? property.Value.Value<string>()
                    : null;
                if (property.Name.StartsWith("skit.", StringComparison.Ordinal) &&
                    !string.IsNullOrEmpty(value))
                {
                    result.Add(property.Name, value);
                }
            }

            return new ReadOnlyDictionary<string, string>(result);
        }
    }
}
