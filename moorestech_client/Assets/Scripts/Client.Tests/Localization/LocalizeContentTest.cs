using System;
using System.IO;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Localization;
using Client.Mod.Texture;
using Core.Master;
using Game.Context;
using Mod.Loader;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Client.Tests.Localization
{
    public class LocalizeContentTest
    {
        private bool hadSavedLanguageCode;
        private string savedLanguageCode;
        private string temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                $"localize-content-{Guid.NewGuid():N}");
            Directory.CreateDirectory(temporaryDirectory);
            PlayerPrefs.SetString(Localize.LanguagePreferenceKey, "japanese");
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Localize.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            // 保存言語を復元し状態漏れを防止
            // Restore the saved language to prevent state leakage
            if (hadSavedLanguageCode)
            {
                PlayerPrefs.SetString(Localize.LanguagePreferenceKey, savedLanguageCode);
            }
            else
            {
                PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            }

            PlayerPrefs.Save();
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true);
            Localize.Initialize();
        }

        [Test]
        public void GetContentResolvesTargetThenEnglishThenSourceThenMarker()
        {
            CreateLocalizationMod();
            var modsResource = new ModsResource(temporaryDirectory);

            Localize.MergeGameDictionaries(
                modsResource,
                new[] { new ModId("author:fallback") });

            Assert.AreEqual("対象", Localize.GetContent("content.target.name"));
            Assert.AreEqual("English", Localize.GetContent("content.english.name"));
            Assert.AreEqual("Source", Localize.GetContent("content.source.name"));
            Assert.AreEqual("[!content.missing.name]", Localize.GetContent("content.missing.name"));
        }

        [Test]
        public void StartupEntryPointUsesRegisteredModOrderAfterMasterLoad()
        {
            var modsResource = ServerContext.GetService<ModsResource>();
            var firstBlock = MasterHolder.BlockMaster.Blocks.Data[0];
            using var itemIds = MasterHolder.ItemMaster.GetItemAllIds().GetEnumerator();
            Assert.IsTrue(itemIds.MoveNext());
            var firstItem = MasterHolder.ItemMaster.GetItemMaster(itemIds.Current);
            var eventCount = 0;
            using var subscription = Localize.OnLanguageChanged.Subscribe(_ => eventCount++);

            var firstResearch = MasterHolder.ResearchMaster.GetAllResearches()[0];
            var firstCategory = MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements[0];
            var firstChallenge = firstCategory.Challenges[0];

            // 起動口の原文と更新通知を検証
            // Verify startup sources and update notifications
            Assert.DoesNotThrow(() => Localize.MergeGameDictionaries(modsResource));
            Assert.AreEqual(
                firstBlock.Name,
                Localize.GetContent(ContentLocalizationKeys.BlockName(firstBlock.BlockGuid)));
            Assert.AreEqual(
                firstItem.Name,
                Localize.GetContent(ContentLocalizationKeys.ItemName(firstItem.ItemGuid)));
            Assert.AreEqual(
                firstResearch.ResearchNodeName,
                Localize.GetContent(ContentLocalizationKeys.ResearchNodeName(firstResearch.ResearchNodeGuid)));
            Assert.AreEqual(
                firstResearch.ResearchNodeDescription,
                Localize.GetContent(ContentLocalizationKeys.ResearchNodeDescription(firstResearch.ResearchNodeGuid)));
            Assert.AreEqual(
                firstCategory.CategoryName,
                Localize.GetContent(ContentLocalizationKeys.ChallengeCategoryName(firstCategory.CategoryGuid)));
            Assert.AreEqual(
                firstChallenge.Title,
                Localize.GetContent(ContentLocalizationKeys.ChallengeTitle(firstChallenge.ChallengeGuid)));
            Assert.AreEqual(
                firstChallenge.Summary,
                Localize.GetContent(ContentLocalizationKeys.ChallengeSummary(firstChallenge.ChallengeGuid)));
            Assert.AreEqual(1, eventCount);
        }

        [Test]
        public void ItemSlotの既定TooltipはItemGuidの翻訳を表示する()
        {
            using var itemIds = MasterHolder.ItemMaster.GetItemAllIds().GetEnumerator();
            Assert.IsTrue(itemIds.MoveNext());
            var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemIds.Current);
            CreateItemLocalizationMod(itemMaster.ItemGuid);
            Localize.MergeGameDictionaries(
                new ModsResource(temporaryDirectory),
                new[] { new ModId("author:item-tooltip") });

            var texture = new Texture2D(1, 1);
            var itemView = new ItemViewData(texture, itemMaster);
            var tooltip = ItemSlotView.GetToolTipText(itemView);

            Assert.AreEqual("翻訳済みアイテム", tooltip);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private void CreateLocalizationMod()
        {
            var modDirectory = Path.Combine(temporaryDirectory, "fallback-mod");
            var masterDirectory = Path.Combine(modDirectory, "master");
            var localizationDirectory = Path.Combine(modDirectory, "localization");
            Directory.CreateDirectory(masterDirectory);
            Directory.CreateDirectory(localizationDirectory);

            // 空セルで解決チェーン各段を分離
            // Separate resolver stages with empty cells
            File.WriteAllText(
                Path.Combine(masterDirectory, "modMeta.json"),
                "{\"id\":\"fallback\",\"name\":\"fallback\",\"version\":\"1.0\",\"author\":\"author\",\"description\":\"test\"}");
            File.WriteAllText(
                Path.Combine(localizationDirectory, "localization.csv"),
                "key,Source,english,japanese\n" +
                "content.target.name,Target Source,Target English,対象\n" +
                "content.english.name,English Source,English,\n" +
                "content.source.name,Source,,\n" +
                "content.missing.name,,,\n");
        }

        private void CreateItemLocalizationMod(Guid itemGuid)
        {
            var modDirectory = Path.Combine(temporaryDirectory, "item-tooltip-mod");
            var masterDirectory = Path.Combine(modDirectory, "master");
            var localizationDirectory = Path.Combine(modDirectory, "localization");
            Directory.CreateDirectory(masterDirectory);
            Directory.CreateDirectory(localizationDirectory);

            File.WriteAllText(
                Path.Combine(masterDirectory, "modMeta.json"),
                "{\"id\":\"item-tooltip\",\"name\":\"item-tooltip\",\"version\":\"1.0\",\"author\":\"author\",\"description\":\"test\"}");
            File.WriteAllText(
                Path.Combine(localizationDirectory, "localization.csv"),
                $"key,Source,english,japanese\nitem.{itemGuid:D}.name,Source,English,翻訳済みアイテム\n");
        }
    }
}
