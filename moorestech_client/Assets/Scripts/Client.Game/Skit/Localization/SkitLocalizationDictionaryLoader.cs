using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Client.Common.Asset;
using Client.Skit.Localization;
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
            using var loadedAsset = await AddressableLoader.LoadAsync<TextAsset>(
                address,
                CancellationToken.None);
            if (loadedAsset?.Asset == null)
            {
                throw new InvalidOperationException(
                    $"Skit localization asset could not be loaded: {address}");
            }

            // Addressableを解放する前に外部JSONを辞書へコピーする
            // Copy the external JSON into a dictionary before releasing the Addressable
            return Parse(address, loadedAsset.Asset.text);
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

            // 非空runtime skitキーのみ公開
            // Publish only non-empty runtime skit keys
            var result = new Dictionary<string, string>();
            foreach (var property in translations.Properties())
            {
                var value = property.Value.Type == JTokenType.String
                    ? property.Value.Value<string>()
                    : null;
                if (property.Name.StartsWith(SkitCommandLocalization.KeyPrefix, StringComparison.Ordinal) &&
                    !string.IsNullOrEmpty(value))
                {
                    result.Add(property.Name, value);
                }
            }

            return new ReadOnlyDictionary<string, string>(result);
        }
    }
}
