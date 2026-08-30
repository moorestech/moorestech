using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Client.Skit.Localization;
using Mooresmaster.Localization.Generated;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Client.Tests.Localization.Skit
{
    public class SkitLocalizationDictionaryCompletenessTest
    {
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
            ["english"] = (208, "2fd4b035c0cdc46e456953d41bd9f55061da6ec946818e434ded3cd26d94565b"),
            ["japanese"] = (208, "c702afae428b48ffd8d8f8f57a65c00250258430805e79890e636016ce533fd4"),
            ["german"] = (208, "e4d5bac0b52a6c509e71a2148301dfdb486761b0bbcf5d1e9bb7f4a8506e067a"),
        };

        // LanguageCatalog由来で全言語を走査し新規言語追加時にbaseline未登録を検知する
        // Drive from LanguageCatalog so adding a language surfaces a missing baseline entry
        private static IEnumerable<string> LanguageCodes()
        {
            return LanguageCatalog.Languages.Select(language => language.Code);
        }

        [TestCaseSource(nameof(LanguageCodes))]
        public void CommandForgeDictionaryKeepsRootFlatTranslationsAndBaselineValues(
            string languageCode)
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
                    AddCommandKeys(validKeys, runtimeTitle, (JObject)command);
                }
            }
            // 両言語を同じ実commandへ照合
            // Match both languages against the same real commands
            var english = (JObject)LoadI18nRoot("english")["translations"];
            var japanese = (JObject)LoadI18nRoot("japanese")["translations"];
            foreach (var requiredKey in RequiredSampleKeys)
            {
                Assert.IsNotNull(english.Property(requiredKey), $"english: {requiredKey}");
                Assert.IsNotNull(japanese.Property(requiredKey), $"japanese: {requiredKey}");
            }
            AssertSkitKeysAreValid(english, validKeys, "english");
            AssertSkitKeysAreValid(japanese, validKeys, "japanese");
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

        private static void AddCommandKeys(
            HashSet<string> keys,
            string title,
            JObject command)
        {
            var commandId = (int)command["id"];
            var type = (string)command["type"];
            if (type == "text" || type == "backgroundSkitText")
            {
                keys.Add($"skit.{title}.{commandId}.body");
                if ((bool?)command["isOverrideCharacterName"] == true)
                    keys.Add($"skit.{title}.{commandId}.overrideCharacterName");
                return;
            }
            if (type != "selection") return;
            AddIfPresent("Option1Tag");
            AddIfPresent("Option2Tag");
            AddIfPresent("Option3Tag");
            #region Internal
            void AddIfPresent(string field)
            {
                if (command[field] != null) keys.Add($"skit.{title}.{commandId}.{field}");
            }
            #endregion
        }
        private static void AssertSkitKeysAreValid(
            JObject translations,
            HashSet<string> validKeys,
            string languageCode)
        {
            foreach (var property in translations.Properties())
            {
                if (!property.Name.StartsWith("skit.", StringComparison.Ordinal)) continue;
                Assert.IsTrue(validKeys.Contains(property.Name), $"{languageCode}: {property.Name}");
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
        private static string CalculateBaselineHash(
            JObject root,
            SortedDictionary<string, string> sortedValues)
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
