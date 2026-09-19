using System;
using System.IO;
using Client.Game.Localization;
using Core.Master;
using Mooresmaster.Model.ChallengesModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Localization.Composition
{
    /// <summary>
    ///     連結ゴーストのWebピン文言が原文収集へ届き、欠落プレースホルダにならないことを検証
    ///     Verifies the chain-ghost web pin wording reaches source collection instead of rendering a missing-key placeholder
    /// </summary>
    public class ChainPreviewSourceTextTest
    {
        private static readonly Guid ChainTutorialGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000c1");

        [Test]
        public void 連結ゴーストのmessageがchallengeTutorialのtextキーで収集される()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            SetChainTutorial("chain ghost pin text");

            var sources = MasterSourceTextCollector.Collect();

            // ピンはtutorialGuidをキーに文言を引くので、同じキーに原文が無ければ[!key]が出る
            // Pins look up wording by tutorialGuid, so a missing source under that key renders [!key]
            var key = $"challengeTutorial.{ChainTutorialGuid:D}.text";
            Assert.IsTrue(sources.ContainsKey(key), $"the chain tutorial text was not collected: {key}");
            Assert.AreEqual("chain ghost pin text", sources[key]);
        }

        // テストmodのchallenges.jsonを連結ゴースト1件だけに差し替えてMasterHolderへ入れる
        // Replace the test mod's tutorials with a single chain preview and install it into MasterHolder
        private static void SetChainTutorial(string message)
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "challenges.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var tutorials = (JArray)json["data"][0]["challenges"][0]["tutorials"];
            var tutorial = (JObject)tutorials[0].DeepClone();
            tutorial["tutorialGuid"] = ChainTutorialGuid.ToString("D");
            tutorial["tutorialType"] = "chainBlockPlacePreview";
            tutorial["tutorialParam"] = new JObject
            {
                ["placingBlockGuid"] = "00000000-0000-0000-0000-000000000014",
                ["chainBlocks"] = new JArray { new JObject { ["blockGuid"] = "00000000-0000-0000-0000-00000000000e", ["offset"] = new JArray(0, 0, 1), ["blockDirection"] = "North" } },
                ["message"] = message,
            };
            tutorials.Clear();
            tutorials.Add(tutorial);

            var master = new ChallengeMaster(json);
            master.Initialize();
            typeof(MasterHolder).GetProperty(nameof(MasterHolder.ChallengeMaster)).GetSetMethod(true).Invoke(null, new object[] { master });
        }
    }
}
