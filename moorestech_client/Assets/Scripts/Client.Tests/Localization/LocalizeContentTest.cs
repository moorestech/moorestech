using System;
using System.IO;
using Client.Localization;
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
            // 保存言語を正確に復元してテスト間の状態漏れを防ぐ
            // Restore the exact saved language to prevent state leakage between tests
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

            // 起動口がitem/block双方の原文と更新通知を公開する
            // Verify the startup entry exposes both item/block sources and one update event
            Assert.DoesNotThrow(() => Localize.MergeGameDictionaries(modsResource));
            Assert.AreEqual(
                firstBlock.Name,
                Localize.GetContent(ContentLocalizationKeys.BlockName(firstBlock.BlockGuid)));
            Assert.AreEqual(
                firstItem.Name,
                Localize.GetContent(ContentLocalizationKeys.ItemName(firstItem.ItemGuid)));
            Assert.AreEqual(1, eventCount);
        }

        private void CreateLocalizationMod()
        {
            var modDirectory = Path.Combine(temporaryDirectory, "fallback-mod");
            var masterDirectory = Path.Combine(modDirectory, "master");
            var localizationDirectory = Path.Combine(modDirectory, "localization");
            Directory.CreateDirectory(masterDirectory);
            Directory.CreateDirectory(localizationDirectory);

            // 空セルを含むfixtureで解決チェーンの各段を分離する
            // Separate every resolver stage with a fixture containing empty cells
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
    }
}
