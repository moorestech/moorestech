using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Client.Skit.Localization;
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
            "skit.100_start_game.31.overrideCharacterName",
            "skit.sample_short.9.Option1Tag", "skit.sample_short.9.Option2Tag",
            "skit.sample_short.9.Option3Tag",
            "skit.200_star_background.1.body",
        };
        // count/hashはTask 8直前commit 7ac9a2decのソート済みCommandForgeキーを正本とする
        // Use sorted CommandForge keys from pre-Task-8 commit 7ac9a2dec as the count/hash baseline
        [TestCase("english", 139, "c0f4c030c88d688a105e0e6b14caf509c9ddb0afa988f519d139e10bc507ffbe")]
        [TestCase("japanese", 204, "b8d2f443d6878c3d9a4e736dee47741a8a93394164cdf73f2f37cd234987a751")]
        public void CommandForgeDictionaryKeepsRootFlatTranslationsAndBaselineKeys(
            string languageCode,
            int expectedBaselineCount,
            string expectedBaselineHash)
        {
            var root = LoadI18nRoot(languageCode);
            var rootNames = new List<string>();
            foreach (var property in root.Properties()) rootNames.Add(property.Name);
            // Task 8直前の辞書形状とキー集合を固定する
            // Freeze the dictionary shape and key set from immediately before Task 8
            CollectionAssert.AreEquivalent(new[] { "locale", "name", "translations" }, rootNames);
            Assert.IsNotEmpty((string)root["locale"]);
            Assert.IsNotEmpty((string)root["name"]);
            var translations = (JObject)root["translations"];
            var baselineKeys = new List<string>();
            foreach (var property in translations.Properties())
            {
                Assert.AreEqual(JTokenType.String, property.Value.Type, property.Name);
                if (property.Name.StartsWith("command.", StringComparison.Ordinal) ||
                    property.Name.StartsWith("master.", StringComparison.Ordinal))
                {
                    baselineKeys.Add(property.Name);
                }
            }
            baselineKeys.Sort(StringComparer.Ordinal);
            Assert.AreEqual(expectedBaselineCount, baselineKeys.Count);
            Assert.AreEqual(expectedBaselineHash, CalculateKeyHash(baselineKeys));
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
            // 両言語のサンプルを同じ実在command集合へ照合する
            // Match both language samples against the same real command set
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
        private static string CalculateKeyHash(List<string> sortedKeys)
        {
            var source = string.Join("\n", sortedKeys) + "\n";
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(source));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
