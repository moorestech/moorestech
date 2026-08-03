using System;
using System.IO;
using Client.Localization;
using Core.Master;
using Mod.Loader;
using Mooresmaster.Localization.Generated;
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
            // 言語と一時modを復元し隔離
            // Restore language and temporary mods to isolate state
            if (hadSavedLanguageCode)
                PlayerPrefs.SetString(Localize.LanguagePreferenceKey, savedLanguageCode);
            else
                PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
            Localize.Initialize();
        }

        [Test]
        public void RecompositionPublishesNewSnapshotAndKeepsOldSnapshotStable()
        {
            var first = CreateResource("first-set", "author:first",
                "key,Source,english,japanese\ncontent.recompose.name,First Source,First English,最初\n");
            Localize.MergeGameDictionaries(first, new[] { new ModId("author:first") });
            var firstRevision = Localize.GetDictionaryRevision();
            Localize.TryGetDictionary("japanese", firstRevision, out var viewBefore);
            var eventCount = 0;
            string notifiedValue = null;
            using var subscription = Localize.OnLanguageChanged.Subscribe(_ =>
            {
                eventCount++;
                notifiedValue = Localize.GetContent(new ContentLocalizationKey("content.recompose.name"));
            });
            var second = CreateResource("second-set", "author:second",
                "key,Source,english,japanese\ncontent.recompose.name,Second Source,Second English,\n");
            Localize.MergeGameDictionaries(second, new[] { new ModId("author:second") });
            var secondRevision = Localize.GetDictionaryRevision();
            Localize.TryGetDictionary("japanese", secondRevision, out var viewAfter);
            Assert.AreEqual("最初", viewBefore["content.recompose.name"]);
            Assert.IsFalse(viewAfter.ContainsKey("content.recompose.name"));
            Assert.AreEqual("Second English", Localize.GetContent(new ContentLocalizationKey("content.recompose.name")));
            Assert.AreNotSame(viewBefore, viewAfter);
            Assert.AreEqual("japanese", Localize.GetCurrentLanguageCode());
            Assert.AreEqual(1, eventCount);
            Assert.AreEqual("Second English", notifiedValue);
            Assert.Greater(secondRevision, firstRevision);
            Assert.IsFalse(Localize.TryGetDictionary("japanese", firstRevision, out _));
        }

        [Test]
        public void FailedRecompositionKeepsPreviousDictionaryAndDoesNotNotify()
        {
            var valid = CreateResource("valid-set", "author:valid",
                "key,Source,english,japanese\ncontent.atomic.name,Source,English,既存\n");
            Localize.MergeGameDictionaries(valid, new[] { new ModId("author:valid") });
            var publishedRevision = Localize.GetDictionaryRevision();
            Localize.TryGetDictionary("japanese", publishedRevision, out var oldSnapshot);
            var eventCount = 0;
            using var subscription = Localize.OnLanguageChanged.Subscribe(_ => eventCount++);

            CreateMod("invalid-set", "partial-mod", "author:partial",
                "key,Source,english,japanese\ncontent.atomic.name,Partial Source,Partial English,途中\n");
            CreateMod("invalid-set", "invalid-mod", "author:invalid",
                "key,Source,klingon\ncontent.atomic.name,Invalid,Qapla\n");
            var invalid = new ModsResource(Path.Combine(temporaryRoot, "invalid-set"));

            Assert.Throws<LocalizationCsvException>(() =>
                Localize.MergeGameDictionaries(
                    invalid,
                    new[] { new ModId("author:partial"), new ModId("author:invalid") }));
            Assert.AreEqual("既存", oldSnapshot["content.atomic.name"]);
            Assert.AreEqual(0, eventCount);
            Assert.AreEqual(publishedRevision, Localize.GetDictionaryRevision());
        }

        [Test]
        public void MasterSourceOverwritesCollidingModSourceForAllCollectedContent()
        {
            using var itemIds = MasterHolder.ItemMaster.GetItemAllIds().GetEnumerator();
            Assert.IsTrue(itemIds.MoveNext());
            var item = MasterHolder.ItemMaster.GetItemMaster(itemIds.Current);
            var block = MasterHolder.BlockMaster.Blocks.Data[0];
            var itemKey = ContentLocalizationKeys.ItemName(item.ItemGuid);
            var blockKey = ContentLocalizationKeys.BlockName(block.BlockGuid);
            var research = MasterHolder.ResearchMaster.GetAllResearches()[0];
            var category = MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements[0];
            var challenge = category.Challenges[0];
            var researchNameKey = ContentLocalizationKeys.ResearchName(research.ResearchNodeGuid);
            var researchDescriptionKey = ContentLocalizationKeys.ResearchDescription(research.ResearchNodeGuid);
            var categoryNameKey = ContentLocalizationKeys.ChallengeCategoryName(category.CategoryGuid);
            var challengeTitleKey = ContentLocalizationKeys.ChallengeTitle(challenge.ChallengeGuid);
            var challengeSummaryKey = ContentLocalizationKeys.ChallengeSummary(challenge.ChallengeGuid);
            var csv =
                "key,Source,english,japanese\n" +
                $"{itemKey.Key},Wrong Item,,\n" +
                $"{blockKey.Key},Wrong Block,,\n" +
                $"{researchNameKey.Key},Wrong Research Name,English Research Name,研究名\n" +
                $"{researchDescriptionKey.Key},Wrong Research Description,,\n" +
                $"{categoryNameKey.Key},Wrong Category,,\n" +
                $"{challengeTitleKey.Key},Wrong Challenge Title,English Challenge Title,チャレンジ名\n" +
                $"{challengeSummaryKey.Key},Wrong Challenge Summary,,\n";
            var modsResource = CreateResource("collision-set", "author:collision", csv);

            Localize.MergeGameDictionaries(modsResource, new[] { new ModId("author:collision") });
            Assert.IsTrue(Localize.TryGetSourceTexts(
                Localize.GetDictionaryRevision(),
                out var sourceDictionary));

            // SourceはMaster正本、選択言語はmod翻訳を優先する
            // Source stays canonical to Master while selected locales prefer mod translations
            Assert.AreEqual(item.Name, Localize.GetContent(itemKey));
            Assert.AreEqual(block.Name, Localize.GetContent(blockKey));
            Assert.AreEqual("研究名", Localize.GetContent(researchNameKey));
            Assert.AreEqual(research.ResearchNodeDescription, Localize.GetContent(researchDescriptionKey));
            Assert.AreEqual(category.CategoryName, Localize.GetContent(categoryNameKey));
            Assert.AreEqual("チャレンジ名", Localize.GetContent(challengeTitleKey));
            Assert.AreEqual(challenge.Summary, Localize.GetContent(challengeSummaryKey));
            Assert.AreEqual(research.ResearchNodeName, sourceDictionary[researchNameKey.Key]);
            Assert.AreEqual(challenge.Title, sourceDictionary[challengeTitleKey.Key]);

            Localize.TrySetLanguage("english");
            Assert.AreEqual("English Research Name", Localize.GetContent(researchNameKey));
            Assert.AreEqual("English Challenge Title", Localize.GetContent(challengeTitleKey));
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

            // ModsResource用mod境界作成
            // Create a minimal mod boundary for ModsResource
            File.WriteAllText(
                Path.Combine(masterDirectory, "modMeta.json"),
                $"{{\"id\":\"{id}\",\"name\":\"{id}\",\"version\":\"1.0\",\"author\":\"{author}\",\"description\":\"test\"}}");
            File.WriteAllText(Path.Combine(localizationDirectory, "localization.csv"), csv);
        }
    }
}
