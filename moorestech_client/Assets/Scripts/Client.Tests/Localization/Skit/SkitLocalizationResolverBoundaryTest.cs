using System;
using System.IO;
using Client.Game.Skit.Localization;
using Client.Localization;
using Client.Skit.Localization;
using Client.Skit.UI;
using Mooresmaster.Loader.CharactersModule;
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

        [Test]
        public void SkitTitleAcceptsOnlyExtensionlessAssetBasename()
        {
            Assert.AreEqual("100_start_game", SkitTitle.FromAssetName("100_start_game"));
            Assert.Throws<ArgumentException>(() => SkitTitle.FromAssetName("100_start_game.json"));
        }

        [Test]
        public void TryGetContentWithoutSourceReturnsCurrentValueAndRejectsMissingKey()
        {
            Localize.Initialize();
            var previousLanguageCode = Localize.GetCurrentLanguageCode();
            Localize.SetLanguage("japanese");

            var foundCurrent = Localize.TryGetContentWithoutSource(
                "ui.mainMenu.playLocally",
                out var current);
            var foundMissing = Localize.TryGetContentWithoutSource(
                "content.missing.name",
                out var missing);
            Localize.SetLanguage(previousLanguageCode);

            Assert.IsTrue(foundCurrent);
            Assert.AreEqual("ローカルでプレイ", current);
            Assert.IsFalse(foundMissing);
            Assert.IsNull(missing);
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
                Assert.AreEqual(Path.GetFileNameWithoutExtension(path), metadataTitle, path);
            }
        }

        [Test]
        public void BackgroundSkitUiDisplaysResolvedSpeakerAndBody()
        {
            var root = new GameObject("BackgroundSkitUiTest");
            var textObject = new GameObject("Text");
            textObject.transform.SetParent(root.transform);
            var ui = root.AddComponent<BackgroundSkitUI>();
            var field = typeof(BackgroundSkitUI).GetField(
                "skitText",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            var textType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            var text = textObject.AddComponent(textType);
            field.SetValue(ui, text);

            ui.SetText("Resolved Speaker", "Resolved Body");

            Assert.AreEqual(
                "Resolved Speaker : Resolved Body",
                text.GetType().GetProperty("text").GetValue(text));
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
