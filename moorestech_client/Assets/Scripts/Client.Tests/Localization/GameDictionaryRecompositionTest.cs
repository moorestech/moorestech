using System;
using System.IO;
using Client.Localization;
using Core.Master;
using Mod.Loader;
using Mooresmaster.LocalizationCsv;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Client.Tests.Localization
{
    public class GameDictionaryRecompositionTest
    {
        private bool hadSavedLanguageCode;
        private string savedLanguageCode;
        private string temporaryRoot;

        [SetUp]
        public void SetUp()
        {
            hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            PlayerPrefs.SetString(Localize.LanguagePreferenceKey, "japanese");
            temporaryRoot = Path.Combine(Path.GetTempPath(), $"game-dictionary-{Guid.NewGuid():N}");
            Directory.CreateDirectory(temporaryRoot);
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Localize.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            // 保存言語と一時modを復元してテスト状態を隔離する
            // Restore the saved language and temporary mods to isolate test state
            if (hadSavedLanguageCode)
                PlayerPrefs.SetString(Localize.LanguagePreferenceKey, savedLanguageCode);
            else
                PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);

            PlayerPrefs.Save();
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
            Localize.Initialize();
        }

        [Test]
        public void RecompositionRemovesStaleTranslationAndKeepsLiveReadOnlyView()
        {
            var first = CreateResource(
                "first-set",
                "author:first",
                "key,Source,english,japanese\ncontent.recompose.name,First Source,First English,最初\n");
            Localize.MergeGameDictionaries(first, new[] { new ModId("author:first") });
            Localize.TryGetDictionary("japanese", out var viewBefore);
            var eventCount = 0;
            using var subscription = Localize.OnLanguageChanged.Subscribe(_ => eventCount++);

            var second = CreateResource(
                "second-set",
                "author:second",
                "key,Source,english,japanese\ncontent.recompose.name,Second Source,Second English,\n");
            Localize.MergeGameDictionaries(second, new[] { new ModId("author:second") });
            Localize.TryGetDictionary("japanese", out var viewAfter);

            Assert.IsFalse(viewAfter.ContainsKey("content.recompose.name"));
            Assert.AreEqual("Second English", Localize.GetContent("content.recompose.name"));
            Assert.AreSame(viewBefore, viewAfter);
            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.AreEqual(1, eventCount);
        }

        [Test]
        public void FailedRecompositionKeepsPreviousDictionaryAndDoesNotNotify()
        {
            var valid = CreateResource(
                "valid-set",
                "author:valid",
                "key,Source,english,japanese\ncontent.atomic.name,Source,English,既存\n");
            Localize.MergeGameDictionaries(valid, new[] { new ModId("author:valid") });
            Localize.TryGetDictionary("japanese", out var liveView);
            var eventCount = 0;
            using var subscription = Localize.OnLanguageChanged.Subscribe(_ => eventCount++);

            CreateMod(
                "invalid-set",
                "partial-mod",
                "author:partial",
                "key,Source,english,japanese\ncontent.atomic.name,Partial Source,Partial English,途中\n");
            CreateMod(
                "invalid-set",
                "invalid-mod",
                "author:invalid",
                "key,Source,klingon\ncontent.atomic.name,Invalid,Qapla\n");
            var invalid = new ModsResource(Path.Combine(temporaryRoot, "invalid-set"));

            Assert.Throws<LocalizationCsvException>(() =>
                Localize.MergeGameDictionaries(
                    invalid,
                    new[] { new ModId("author:partial"), new ModId("author:invalid") }));
            Assert.AreEqual("既存", liveView["content.atomic.name"]);
            Assert.AreEqual(0, eventCount);
        }

        [Test]
        public void MasterSourceOverwritesCollidingModSourceForItemAndBlockKeys()
        {
            using var itemIds = MasterHolder.ItemMaster.GetItemAllIds().GetEnumerator();
            Assert.IsTrue(itemIds.MoveNext());
            var item = MasterHolder.ItemMaster.GetItemMaster(itemIds.Current);
            var block = MasterHolder.BlockMaster.Blocks.Data[0];
            var itemKey = ContentLocalizationKeys.ItemName(item.ItemGuid);
            var blockKey = ContentLocalizationKeys.BlockName(block.BlockGuid);
            var csv = $"key,Source,english,japanese\n{itemKey},Wrong Item,,\n{blockKey},Wrong Block,,\n";
            var modsResource = CreateResource("collision-set", "author:collision", csv);

            Localize.MergeGameDictionaries(modsResource, new[] { new ModId("author:collision") });

            Assert.AreEqual(item.Name, Localize.GetContent(itemKey));
            Assert.AreEqual(block.Name, Localize.GetContent(blockKey));
        }

        [Test]
        public void PublicEntryPointRejectsResourceWhoseIdsDifferFromMasterOrder()
        {
            var emptySet = Path.Combine(temporaryRoot, "empty-set");
            Directory.CreateDirectory(emptySet);
            var mismatchedResource = new ModsResource(emptySet);
            var eventCount = 0;
            using var subscription = Localize.OnLanguageChanged.Subscribe(_ => eventCount++);

            Assert.Throws<InvalidOperationException>(() =>
                Localize.MergeGameDictionaries(mismatchedResource));
            Assert.AreEqual(0, eventCount);
        }

        private ModsResource CreateResource(string setName, string fullModId, string csv)
        {
            var setDirectory = Path.Combine(temporaryRoot, setName);
            CreateMod(setName, "mod", fullModId, csv);
            return new ModsResource(setDirectory);
        }

        private void CreateMod(string setName, string directoryName, string fullModId, string csv)
        {
            var setDirectory = Path.Combine(temporaryRoot, setName);
            var modDirectory = Path.Combine(setDirectory, directoryName);
            var masterDirectory = Path.Combine(modDirectory, "master");
            var localizationDirectory = Path.Combine(modDirectory, "localization");
            Directory.CreateDirectory(masterDirectory);
            Directory.CreateDirectory(localizationDirectory);
            var separator = fullModId.IndexOf(':');
            var author = fullModId.Substring(0, separator);
            var id = fullModId.Substring(separator + 1);

            // 実ModsResourceを通すため最小mod境界fixtureを作る
            // Create a minimal mod-boundary fixture consumed by the real ModsResource
            File.WriteAllText(
                Path.Combine(masterDirectory, "modMeta.json"),
                $"{{\"id\":\"{id}\",\"name\":\"{id}\",\"version\":\"1.0\",\"author\":\"{author}\",\"description\":\"test\"}}");
            File.WriteAllText(Path.Combine(localizationDirectory, "localization.csv"), csv);
        }
    }
}
