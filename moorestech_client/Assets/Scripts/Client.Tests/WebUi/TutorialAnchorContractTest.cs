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

        #endregion
    }
}
