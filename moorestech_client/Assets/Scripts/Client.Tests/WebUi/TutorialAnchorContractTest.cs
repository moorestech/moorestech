using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.Tutorial.UIHighlight;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi
{
    // マスタchallenges.jsonのanchorId直書き値とWeb側単一ソース（フィクスチャ）を突合する設定者向けツールテスト
    // Configurer-facing tool test cross-checking anchorIds written in master challenges.json against the Web-side single source (fixture)
    public class TutorialAnchorContractTest
    {
        // FromItemIdが生成するprefixがWeb側の動的prefix定義と一致すること
        // FromItemId's generated prefix must match the Web-side dynamic prefix definition
        [Test]
        public void ItemAnchorPrefixMatchesWebFixture()
        {
            var fixture = LoadFixture();
            var expectedPrefix = fixture["dynamicPrefixes"]["recipeItem"].Value<string>();

            Assert.AreEqual(expectedPrefix, TutorialAnchorIdMapper.ItemAnchorPrefix);
            Assert.IsTrue(TutorialAnchorIdMapper.FromItemId(42).StartsWith(expectedPrefix));
        }

        // 全modのchallenges.jsonが直書きするanchorIdが、Web側の静的ID一覧か動的prefixに解決すること
        // Every anchorId written across all mods' challenges.json must resolve to a Web-side static ID or dynamic prefix
        [Test]
        public void AllModAnchorIdsResolveToWebVocabulary()
        {
            var masterRoot = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../moorestech_master"));
            if (!Directory.Exists(masterRoot))
            {
                Assert.Ignore($"moorestech_master repository not found at {masterRoot}");
                return;
            }

            var anchorIds = CollectAnchorIds(masterRoot);
            if (anchorIds.Count == 0)
            {
                // 兄弟checkoutが別セッションにより互換外コミットへ移動している場合があるため、空は環境要因としてスキップする
                // The sibling checkout may have been moved to an incompatible commit by another session, so treat empty as environmental
                Assert.Ignore("No anchorId found across any mod's challenges.json (sibling checkout may be on an unrelated commit)");
                return;
            }

            var fixture = LoadFixture();
            var staticIds = fixture["staticIds"].Select(t => t.Value<string>()).ToHashSet();
            var dynamicPrefixes = ((JObject)fixture["dynamicPrefixes"]).Properties()
                .ToDictionary(p => p.Name, p => p.Value.Value<string>());

            foreach (var anchorId in anchorIds)
            {
                // 解決セレクタが空白区切りトークン一致のため、空白入りIDはどの要素にも当たらない
                // The resolver matches whitespace-separated tokens, so a whitespace-bearing ID can never hit an element
                Assert.IsFalse(anchorId.Any(char.IsWhiteSpace), $"'{anchorId}' must not contain whitespace");

                if (staticIds.Contains(anchorId)) continue;

                var matched = dynamicPrefixes.FirstOrDefault(entry => anchorId.StartsWith(entry.Value, StringComparison.Ordinal));
                Assert.IsNotNull(matched.Value, $"'{anchorId}' does not resolve to any Web-side static anchor ID or dynamic prefix");
                AssertSuffix(matched.Key, anchorId.Substring(matched.Value.Length), anchorId);
            }
        }

        // 動的prefixごとに接尾辞の種別（guid/整数/小文字自由語）を検査する。Web側生成関数の書式と揃える
        // Check each dynamic prefix's suffix kind (guid / integer / lowercase free word) to match the Web-side generators
        private static void AssertSuffix(string prefixKey, string suffix, string anchorId)
        {
            switch (prefixKey)
            {
                case "researchNode":
                case "challengeNode":
                case "inventoryItem":
                    Assert.IsTrue(Guid.TryParseExact(suffix, "D", out _), $"'{anchorId}' must end with a guid");
                    Assert.AreEqual(suffix.ToLowerInvariant(), suffix, $"'{anchorId}' must be lowercased");
                    break;
                case "recipeItem":
                case "equipmentSlot":
                    Assert.IsTrue(int.TryParse(suffix, out _), $"'{anchorId}' must end with an integer");
                    break;
                case "buildMenuEntry":
                    Assert.IsTrue(suffix.Length > 0, $"'{anchorId}' must have a suffix");
                    Assert.AreEqual(suffix.ToLowerInvariant(), suffix, $"'{anchorId}' must be lowercased");
                    break;
                default:
                    Assert.Fail($"unknown dynamic prefix '{prefixKey}' has no suffix rule");
                    break;
            }
        }

        private static JObject LoadFixture()
        {
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", "tutorial_anchor_ids.json");
            return JObject.Parse(File.ReadAllText(path));
        }

        private static List<string> CollectAnchorIds(string masterRoot)
        {
            var result = new List<string>();
            foreach (var serverDir in Directory.GetDirectories(masterRoot, "server*"))
            {
                var modsDir = Path.Combine(serverDir, "mods");
                if (!Directory.Exists(modsDir)) continue;

                foreach (var modDir in Directory.GetDirectories(modsDir))
                {
                    var challengesPath = Path.Combine(modDir, "master", "challenges.json");
                    if (!File.Exists(challengesPath)) continue;

                    var json = JToken.Parse(File.ReadAllText(challengesPath));
                    result.AddRange(json.SelectTokens("$..highLightAnchorId").Select(t => t.Value<string>()));
                    result.AddRange(json.SelectTokens("$..fromAnchorId").Select(t => t.Value<string>()));
                    result.AddRange(json.SelectTokens("$..toAnchorId").Select(t => t.Value<string>()));
                }
            }

            return result;
        }
    }
}
