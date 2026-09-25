using System.IO;
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

        private static JObject LoadFixture()
        {
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", "tutorial_anchor_ids.json");
            return JObject.Parse(File.ReadAllText(path));
        }

    }
}
