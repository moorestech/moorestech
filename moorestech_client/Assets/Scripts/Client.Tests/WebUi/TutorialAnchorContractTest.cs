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
    // TutorialAnchorIdMapper・Web側単一ソース・マスタchallenges.jsonの三者を突合する
    // Cross-checks TutorialAnchorIdMapper, the Web-side single source, and master challenges.json
    public class TutorialAnchorContractTest
    {
        // マッパーの静的出力アンカーID全件がWeb側フィクスチャに存在すること
        // Every statically mapped anchor ID must exist in the Web-side fixture
        [Test]
        public void MapperStaticAnchorIdsExistInWebFixture()
        {
            var fixture = LoadFixture();
            var staticIds = fixture["staticIds"].Select(t => t.Value<string>()).ToHashSet();

            foreach (var anchorId in TutorialAnchorIdMapper.AllMappedAnchorIds)
            {
                Assert.IsTrue(staticIds.Contains(anchorId), $"'{anchorId}' is missing from tutorial_anchor_ids.json staticIds");
            }
        }

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

        // 全modのchallenges.jsonが宣言するhighLightUIObjectIdが、マッパーの辞書キーに存在すること
        // Every highLightUIObjectId declared across all mods' challenges.json must be a known mapper key
        [Test]
        public void AllModHighLightUIObjectIdsAreKnownToMapper()
        {
            var masterRoot = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../moorestech_master"));
            if (!Directory.Exists(masterRoot))
            {
                Assert.Ignore($"moorestech_master repository not found at {masterRoot}");
                return;
            }

            var uiObjectIds = CollectHighLightUIObjectIds(masterRoot);
            Assert.IsNotEmpty(uiObjectIds, "No highLightUIObjectId found across any mod's challenges.json");

            foreach (var uiObjectId in uiObjectIds)
            {
                Assert.IsTrue(TutorialAnchorIdMapper.IsKnownUiObjectId(uiObjectId), $"'{uiObjectId}' is not a known key in TutorialAnchorIdMapper");
            }
        }

        // 動的uiObjectId書式（buildMenuBlock:/researchNode:）がWeb側動的prefixへ変換されること
        // Dynamic uiObjectId forms must map onto the Web-side dynamic prefixes
        [Test]
        public void DynamicUiObjectIdsMapToWebDynamicPrefixes()
        {
            var fixture = LoadFixture();
            var buildMenuPrefix = fixture["dynamicPrefixes"]["buildMenuEntry"].Value<string>();
            var researchPrefix = fixture["dynamicPrefixes"]["researchNode"].Value<string>();

            var blockAnchor = TutorialAnchorIdMapper.FromUiObjectId("buildMenuBlock:934C0EF9-B76E-4058-8FC8-0AD74AFBDCD0");
            Assert.AreEqual($"{buildMenuPrefix}block-934c0ef9-b76e-4058-8fc8-0ad74afbdcd0", blockAnchor);

            var researchAnchor = TutorialAnchorIdMapper.FromUiObjectId("researchNode:837E9697-8586-406E-A0F6-16A010050218");
            Assert.AreEqual($"{researchPrefix}837e9697-8586-406e-a0f6-16a010050218", researchAnchor);
        }

        // 全modのchallenges.jsonが宣言するuiDragGuideのfrom/toが既知のuiObjectIdであること
        // Every uiDragGuide from/to declared across all mods must be a known uiObjectId
        [Test]
        public void AllModDragGuideUiObjectIdsAreKnownToMapper()
        {
            var masterRoot = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../moorestech_master"));
            if (!Directory.Exists(masterRoot))
            {
                Assert.Ignore($"moorestech_master repository not found at {masterRoot}");
                return;
            }

            foreach (var uiObjectId in CollectDragGuideUiObjectIds(masterRoot))
            {
                Assert.IsTrue(TutorialAnchorIdMapper.IsKnownUiObjectId(uiObjectId), $"'{uiObjectId}' is not a known key in TutorialAnchorIdMapper");
            }
        }

        #region Internal

        private static JObject LoadFixture()
        {
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", "tutorial_anchor_ids.json");
            return JObject.Parse(File.ReadAllText(path));
        }

        private static List<string> CollectHighLightUIObjectIds(string masterRoot)
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
                    result.AddRange(json.SelectTokens("$..highLightUIObjectId").Select(t => t.Value<string>()));
                }
            }

            return result;
        }

        private static List<string> CollectDragGuideUiObjectIds(string masterRoot)
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
                    result.AddRange(json.SelectTokens("$..fromUIObjectId").Select(t => t.Value<string>()));
                    result.AddRange(json.SelectTokens("$..toUIObjectId").Select(t => t.Value<string>()));
                }
            }

            return result;
        }

        #endregion
    }
}
