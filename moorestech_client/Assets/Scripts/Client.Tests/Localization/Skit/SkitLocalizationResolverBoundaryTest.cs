using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Client.Game.Skit.Localization;
using Client.Localization;
using Client.Skit.Localization;
using Mooresmaster.Loader.CharactersModule;
using Mooresmaster.Localization.Generated;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Localization.Skit
{
    public class SkitLocalizationResolverBoundaryTest
    {
        [Test]
        public void LoaderParserKeepsOnlyNonEmptySkitEntries()
        {
            const string json = @"{
  ""locale"": ""ja"",
  ""name"": ""日本語"",
  ""translations"": {
    ""skit.opening.1.body"": ""表示文"",
    ""skit.opening.2.body"": """",
    ""command.text.name"": ""テキスト"",
    ""master.characters.chr_001"": ""主人公""
  }
}";

            var dictionary = SkitLocalizationDictionaryLoader.Parse(
                "Vanilla/Skit/i18n/japanese",
                json);

            CollectionAssert.AreEquivalent(
                new[] { "skit.opening.1.body" },
                dictionary.Keys);
        }

        [Test]
        public void LoaderParserReportsAddressForInvalidExternalJson()
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                SkitLocalizationDictionaryLoader.Parse(
                    "Vanilla/Skit/i18n/japanese",
                    "{ invalid"));

            StringAssert.Contains("Vanilla/Skit/i18n/japanese", exception.Message);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("opening.json")]
        [TestCase("folder/opening")]
        [TestCase("folder\\opening")]
        public void SkitTitleRejectsValuesThatAreNotAssetBasenames(string invalidName)
        {
            Assert.Throws<ArgumentException>(() => SkitTitle.FromAssetName(invalidName));
        }

        [Test]
        public void SkitTitleReturnsValidAssetBasename()
        {
            Assert.AreEqual("100_start_game", SkitTitle.FromAssetName("100_start_game"));
        }

        [TestCase(@"{ ""translations"": { ""skit.opening.1.body"": ""first"", ""skit.opening.1.body"": ""second"" } }")]
        [TestCase(@"{ ""translations"": {}, ""translations"": { ""skit.opening.1.body"": ""second"" } }")]
        public void LoaderParserRejectsDuplicatePropertiesWithAddress(string json)
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                SkitLocalizationDictionaryLoader.Parse(
                    "Vanilla/Skit/i18n/japanese",
                    json));

            StringAssert.Contains("Vanilla/Skit/i18n/japanese", exception.Message);
        }

        [Test]
        public void SkitScopeResolvesCurrentLanguageAndNeverReachesSourceStage()
        {
            Localize.Initialize();
            var previousLanguageCode = Localize.GetCurrentLanguageCode();
            Localize.TrySetLanguage("japanese");

            // Skit解決が読む2段だけを実辞書から取り、source段を持つGetContentと突き合わせる
            // Build the two stages skit resolution reads and compare them against GetContent, which has the Source stage
            Localize.TryGetDictionary("japanese", out var japanese);
            Localize.TryGetDictionary(Localize.DefaultLanguageCode, out var english);
            var scope = new SkitLocalizationScope(
                new Dictionary<string, string>(japanese),
                new Dictionary<string, string>(english));
            var current = scope.Resolve("ui.mainMenu.playLocally", "JSON Source");
            var missing = scope.Resolve("content.missing.name", "JSON Source");
            var missingWithSourceStage = Localize.GetContent(new ContentLocalizationKey("content.missing.name"));
            Localize.TrySetLanguage(previousLanguageCode);

            Assert.AreEqual("ローカルでプレイ", current);
            Assert.AreEqual("JSON Source", missing);
            Assert.AreEqual("[!content.missing.name]", missingWithSourceStage);
        }

        [TestCase("English Body", "English Body")]
        [TestCase("", "JSON Source")]
        public async Task SkitResolutionStopsAtEnglishWithoutReadingSourcePseudoLocale(
            string englishBody,
            string expected)
        {
            const string skitKey = "skit.opening.7.body";
            var loader = new FakeSkitDictionaryLoader();
            loader.Set("japanese", skitKey, "");
            loader.Set("english", skitKey, "");
            var source = new FakeSkitLocalizationSource();
            source.Set("japanese", skitKey, "");
            source.Set("english", skitKey, englishBody);

            // source擬似localeへ値があっても解決段に現れないことを示す
            // Prove the Source pseudo-locale never appears in the resolution stages even when it holds a value
            source.Set(Localize.SourcePseudoLocale, skitKey, "Source Pseudo Body");
            using var resolver = new SkitLocalizationResolver(loader, source);
            await resolver.PrepareAsync("opening");

            Assert.AreEqual(
                expected,
                resolver.ResolveCommandField(7, "body", "JSON Source"));
        }

        [Test]
        public void CharacterLoaderRejectsMissingRequiredCharacterGuid()
        {
            var json = JToken.Parse(@"{
  ""data"": [{
    ""characterId"": ""chr_001"",
    ""displayName"": ""Yori"",
    ""modelAddresablePath"": ""Vanilla/Character/Chr001"",
    ""skitModelAddresablePath"": ""Vanilla/Character/SkitChr001""
  }]
}");

            var exception = Assert.Catch<Exception>(() => CharactersLoader.Load(json));

            Assert.AreEqual("MooresmasterLoaderException", exception.GetType().Name);
        }

        [Test]
        public void AddressableSkitDataContainsNoLegacyOverrideFieldTypo()
        {
            var skitRoot = Path.Combine(
                Application.dataPath,
                "AddressableResources",
                "Skit");

            foreach (var path in Directory.GetFiles(skitRoot, "*.json", SearchOption.AllDirectories))
            {
                StringAssert.DoesNotContain("overideCharacterName", File.ReadAllText(path), path);
            }
        }

        [Test]
        public void SkitMetadataTitleMatchesAssetBasenameWithoutDrivingRuntimeKeys()
        {
            var skitRoot = Path.Combine(
                Application.dataPath,
                "AddressableResources",
                "Skit",
                "skits");

            foreach (var path in Directory.GetFiles(skitRoot, "*.json"))
            {
                var metadataTitle = (string)JObject.Parse(File.ReadAllText(path))["meta"]?["title"];
                var runtimeTitle = SkitTitle.FromAssetName(Path.GetFileNameWithoutExtension(path));
                Assert.AreEqual(runtimeTitle, metadataTitle, path);
            }
        }
    }
}
