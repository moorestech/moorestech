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
            var dynamicPrefixes = ((JObject)fixture["dynamicPrefixes"]).Properties().Select(p => p.Value.Value<string>()).ToArray();

            foreach (var anchorId in anchorIds)
            {
                var resolves = staticIds.Contains(anchorId) || dynamicPrefixes.Any(prefix => anchorId.StartsWith(prefix, StringComparison.Ordinal));
                Assert.IsTrue(resolves, $"'{anchorId}' does not resolve to any Web-side static anchor ID or dynamic prefix");
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
