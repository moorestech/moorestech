using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Client.Localization;
using Client.Skit.Localization;
using Mooresmaster.Localization.Generated;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Client.Tests.Localization.Skit
{
    public class SkitLocalizationDictionaryCompletenessTest
    {
        private const string CanonicalKeyLanguageCode = "japanese";
        private const string EnglishMirrorLanguageCode = "german";
        private static readonly string[] RequiredSampleKeys =
        {
            "skit.100_start_game.1.body", "skit.100_start_game.2.body",
            "skit.100_start_game.3.body", "skit.100_start_game.4.body",
            "skit.sample_short.9.Option1Tag", "skit.sample_short.9.Option2Tag",
            "skit.sample_short.9.Option3Tag",
            "skit.200_star_background.1.body",
        };

        // count/hashは列車をworldObjectEnable側へ移した後のroot値とソート済みCommandForge key/valueを正本とする
        // Baseline is the root values and sorted CommandForge key/value pairs after trains moved to worldObjectEnable
        private static readonly Dictionary<string, (int Count, string Hash)> Baselines = new()
        {
            [Localize.DefaultLanguageCode] = (162, "4ae1de4d73b56470e9abd475b3ecd07692e17305709b08c9f00bf07c2d8f5e76"),
            [CanonicalKeyLanguageCode] = (162, "4a3ed5dd98c690235baa09cb6b1aa36250d4e23e94de03f29215f409d7a68315"),
            [EnglishMirrorLanguageCode] = (162, "2534c6dd29dbbb302173017d2dce5aa6a40660cbc94142190bf3dc76c43fec55"),
        };

        // LanguageCatalog由来で全言語を走査し新規言語追加時にbaseline未登録を検知する
        // Drive from LanguageCatalog so adding a language surfaces a missing baseline entry
        private static IEnumerable<string> LanguageCodes()
        {
            return LanguageCatalog.Languages.Select(language => language.Code);
        }

        [TestCaseSource(nameof(LanguageCodes))]
        public void CommandForgeDictionaryKeepsRootFlatTranslationsAndBaselineValues(string languageCode)
        {
            var (expectedBaselineCount, expectedBaselineHash) = Baselines[languageCode];
            var root = LoadI18nRoot(languageCode);
            var rootNames = new List<string>();
            foreach (var property in root.Properties()) rootNames.Add(property.Name);
            // 直前辞書の形状・root値を固定
            // Pin the preceding dictionary shape and root values
            CollectionAssert.AreEquivalent(new[] { "locale", "name", "translations" }, rootNames);
            Assert.IsNotEmpty((string)root["locale"]);
            Assert.IsNotEmpty((string)root["name"]);
            var translations = (JObject)root["translations"];
            var japaneseTranslations = (JObject)LoadI18nRoot(CanonicalKeyLanguageCode)["translations"];
            // 全言語の辞書キーを日本語と照合する
            // Match every dictionary's keys against Japanese
            CollectionAssert.AreEquivalent(
                japaneseTranslations.Properties().Select(property => property.Name),
                translations.Properties().Select(property => property.Name));
            // 独語の翻訳値は英語複写を維持する
            // Keep German translation values copied from English
            if (languageCode == EnglishMirrorLanguageCode)
                Assert.IsTrue(JToken.DeepEquals(LoadI18nRoot(Localize.DefaultLanguageCode)["translations"], translations));
            var baselineValues = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in translations.Properties())
            {
                Assert.AreEqual(JTokenType.String, property.Value.Type, property.Name);
                if (property.Name.StartsWith("command.", StringComparison.Ordinal) ||
                    property.Name.StartsWith("master.", StringComparison.Ordinal))
                {
                    baselineValues.Add(property.Name, (string)property.Value);
                }
            }
            Assert.AreEqual(expectedBaselineCount, baselineValues.Count);
            Assert.AreEqual(expectedBaselineHash, CalculateBaselineHash(root, baselineValues));
        }

        [Test]
        public void RuntimeSkitKeysMatchAssetBasenamesAndSchemaFields()
        {
            var validKeys = new HashSet<string>(StringComparer.Ordinal);
            var skitRoot = Path.Combine(Application.dataPath, "AddressableResources", "Skit", "skits");
            foreach (var path in Directory.GetFiles(skitRoot, "*.json"))
            {
                var root = JObject.Parse(File.ReadAllText(path));
                var runtimeTitle = SkitTitle.FromAssetName(Path.GetFileNameWithoutExtension(path));
                Assert.AreEqual(runtimeTitle, (string)root["meta"]?["title"], path);
                foreach (var command in (JArray)root["commands"])
                {
                    AddCommandKeys(runtimeTitle, (JObject)command);
                }
            }
            // 全言語を同じ実commandへ照合
            // Match every language against the same real commands
            foreach (var languageCode in LanguageCodes())
            {
                var translations = (JObject)LoadI18nRoot(languageCode)["translations"];
                foreach (var requiredKey in RequiredSampleKeys)
                    Assert.IsNotNull(translations.Property(requiredKey), $"{languageCode}: {requiredKey}");
                AssertSkitKeysAreValid(translations, languageCode);
            }
            #region Internal

            void AddCommandKeys(string title, JObject command)
            {
                var commandId = (int)command["id"];
                var type = (string)command["type"];
                if (type == "text" || type == "backgroundSkitText")
                {
                    validKeys.Add($"skit.{title}.{commandId}.body");
                    if ((bool?)command["isOverrideCharacterName"] == true)
                        validKeys.Add($"skit.{title}.{commandId}.overrideCharacterName");
                    return;
                }
                if (type != "selection") return;
                AddIfPresent("Option1Tag");
                AddIfPresent("Option2Tag");
                AddIfPresent("Option3Tag");

                void AddIfPresent(string field)
                {
                    if (command[field] != null) validKeys.Add($"skit.{title}.{commandId}.{field}");
                }
            }

            void AssertSkitKeysAreValid(JObject translations, string languageCode)
            {
                foreach (var property in translations.Properties())
                {
                    if (!property.Name.StartsWith("skit.", StringComparison.Ordinal)) continue;
                    Assert.IsTrue(validKeys.Contains(property.Name), $"{languageCode}: {property.Name}");
                }
            }

            #endregion
        }

        [Test]
        public void AllTranslationValuesAreNonEmpty()
        {
            var i18nRoot = Path.Combine(
                Application.dataPath,
                "AddressableResources",
                "Skit",
                "i18n");
            var dictionaryPaths = Directory.GetFiles(i18nRoot, "*.json");
            Assert.IsNotEmpty(dictionaryPaths);

            // 空文字は欠落扱いで英語へ落ちるため翻訳漏れとして弾く
            // Empty values fall through to English, so reject them as missed translations
            foreach (var path in dictionaryPaths)
            {
                var translations = (JObject)JObject.Parse(File.ReadAllText(path))["translations"];
                foreach (var property in translations.Properties())
                {
                    Assert.IsFalse(
                        string.IsNullOrEmpty((string)property.Value),
                        $"{Path.GetFileName(path)}: {property.Name} が空文字");
                }
            }
        }
        private static JObject LoadI18nRoot(string languageCode)
        {
            return JObject.Parse(File.ReadAllText(GetI18nPath(languageCode)));
        }
        private static string GetI18nPath(string languageCode)
        {
            return Path.Combine(
                Application.dataPath,
                "AddressableResources",
                "Skit",
                "i18n",
                languageCode + ".json");
        }
        private static string CalculateBaselineHash(JObject root, SortedDictionary<string, string> sortedValues)
        {
            var canonical = new StringBuilder();
            AppendPair("locale", (string)root["locale"]);
            AppendPair("name", (string)root["name"]);
            foreach (var pair in sortedValues) AppendPair(pair.Key, pair.Value);
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

            #region Internal

            void AppendPair(string key, string value)
            {
                canonical.Append(key.Length).Append(':').Append(key);
                canonical.Append(value.Length).Append(':').Append(value);
            }

            #endregion
        }
    }
}
