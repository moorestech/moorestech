using System;
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
        public void CollectorContainsEveryResearchCategoryAndChallengeSource()
        {
            var expected = BuildExpectedContentSources();
            var actual = new Dictionary<string, string>();
            foreach (var pair in MasterSourceTextCollector.Collect())
            {
                if (IsResearchOrChallengeKey(pair.Key)) actual.Add(pair.Key, pair.Value);
            }

            // 件数・キー集合・値を別々に照合して欠落と上書きを検出する
            // Compare counts, key sets, and values separately to catch omissions and overwrites
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

            // Builder実装に依存しない手計算キーで全研究を列挙する
            // Enumerate every research using hand-derived keys independent from the builders
            foreach (var research in MasterHolder.ResearchMaster.GetAllResearches())
            {
                expected.Add($"research.{research.ResearchNodeGuid:D}.name", research.ResearchNodeName);
                expected.Add($"research.{research.ResearchNodeGuid:D}.description", research.ResearchNodeDescription);
            }

            // 全カテゴリと全チャレンジをAddし、Guid重複もテスト失敗にする
            // Add every category and challenge so duplicate GUIDs also fail the test
            foreach (var category in MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements)
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

        private static bool IsResearchOrChallengeKey(string key)
        {
            return key.StartsWith("research.", StringComparison.Ordinal) ||
                   key.StartsWith("challenge.", StringComparison.Ordinal) ||
                   key.StartsWith("challengeCategory.", StringComparison.Ordinal);
        }
    }
}
