using System.Collections.Generic;
using Client.Localization;
using Core.Master;
using Game.Context;
using Mod.Loader;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Localization.MasterSource
{
    public class MasterSourceTextCollectorTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Localize.Initialize();
        }

        [Test]
        public void CollectorMatchesEveryMasterSource()
        {
            var expected = BuildExpectedContentSources();
            var actual = MasterSourceTextCollector.Collect();

            // 件数・キー・値で欠落と上書きを検出
            // Use counts, keys, and values to detect omissions and overwrites
            Assert.AreEqual(expected.Count, actual.Count);
            CollectionAssert.AreEquivalent(expected.Keys, actual.Keys);
            foreach (var pair in expected)
            {
                Assert.AreEqual(pair.Value, actual[pair.Key], pair.Key);
            }
        }

        [Test]
        public void SourceSnapshotMatchesEveryNonEmptyMasterSourceAndOmitsEmptySources()
        {
            var expected = BuildExpectedContentSources();
            var modsResource = ServerContext.GetService<ModsResource>();

            Localize.MergeGameDictionaries(modsResource);
            Assert.IsTrue(Localize.TryGetDictionary(Localize.SourcePseudoLocale, out var source));

            // 空原文はcanonical欠落、それ以外はMaster原文との完全一致を要求する
            // Empty sources are canonical omissions; every other value must exactly match Master
            foreach (var pair in expected)
            {
                if (string.IsNullOrEmpty(pair.Value))
                {
                    Assert.IsFalse(source.ContainsKey(pair.Key), pair.Key);
                    continue;
                }

                Assert.AreEqual(pair.Value, source[pair.Key], pair.Key);
            }
        }

        private static Dictionary<string, string> BuildExpectedContentSources()
        {
            var expected = new Dictionary<string, string>();

            // 全アイテムと全ブロックを正本から列挙する
            // Enumerate every item and block from their canonical masters
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var item = MasterHolder.ItemMaster.GetItemMaster(itemId);
                expected.Add($"item.{item.ItemGuid:D}.name", item.Name);
            }
            foreach (var block in MasterHolder.BlockMaster.Blocks.Data)
            {
                expected.Add($"block.{block.BlockGuid:D}.name", block.Name);
            }

            // 全分類を必須Guidから列挙
            // Enumerate every classification from required GUIDs
            foreach (var category in MasterHolder.BuildMenuCategoryMaster.Categories)
            {
                expected.Add($"buildMenuCategory.{category.CategoryGuid:D}.name", category.Name);
                foreach (var subCategory in category.SubCategories)
                {
                    expected.Add(
                        $"buildMenuSubCategory.{subCategory.SubCategoryGuid:D}.name",
                        subCategory.Name);
                }
            }

            // 全話者名を必須Guidから列挙
            // Enumerate every speaker name from required GUIDs
            foreach (var character in MasterHolder.CharacterMaster.Characters.Data)
            {
                expected.Add($"character.{character.CharacterGuid:D}.name", character.DisplayName);
            }

            // 辞書化前の正本配列を使い、研究Guid重複による欠落も検出する
            // Use the canonical pre-dictionary array to catch loss from duplicate research GUIDs
            foreach (var research in MasterHolder.ResearchMaster.Research.Data)
            {
                expected.Add($"research.{research.ResearchNodeGuid:D}.name", research.ResearchNodeName);
                expected.Add($"research.{research.ResearchNodeGuid:D}.description", research.ResearchNodeDescription);
            }

            // 全カテゴリと全チャレンジをAddし、Guid重複もテスト失敗にする
            // Add every category and challenge so duplicate GUIDs also fail the test
            foreach (var category in MasterHolder.ChallengeMaster.Challenges.Data)
            {
                expected.Add($"challengeCategory.{category.CategoryGuid:D}.name", category.CategoryName);
                expected.Add($"challengeCategory.{category.CategoryGuid:D}.description", category.CategoryDescription);
                foreach (var challenge in category.Challenges)
                {
                    expected.Add($"challenge.{challenge.ChallengeGuid:D}.title", challenge.Title);
                    expected.Add($"challenge.{challenge.ChallengeGuid:D}.summary", challenge.Summary);
                }
            }

            return expected;
        }
    }
}
