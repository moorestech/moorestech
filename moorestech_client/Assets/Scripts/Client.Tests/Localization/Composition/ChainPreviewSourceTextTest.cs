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
    ///     連結ゴーストのピン文言が原文収集に届くか検証
    ///     Verifies chain-ghost pin wording reaches source collection
    /// </summary>
    public class ChainPreviewSourceTextTest
    {
        private static readonly Guid ChainTutorialGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000c1");

        private ChallengeMaster _originalChallengeMaster;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _originalChallengeMaster = MasterHolder.ChallengeMaster;
        }

        [TearDown]
        public void TearDown()
        {
            SetChallengeMaster(_originalChallengeMaster);
        }

        [Test]
        public void 連結ゴーストのmessageがchallengeTutorialのtextキーで収集される()
        {
            const string message = "chain ghost pin text";
            SetChainTutorial();

            var sources = MasterSourceTextCollector.Collect();

            // ピンはtutorialGuidをキーに文言を引くので、同じキーに原文が無ければ[!key]が出る
            // Pins look up wording by tutorialGuid, so a missing source under that key renders [!key]
            var key = $"challengeTutorial.{ChainTutorialGuid:D}.text";
            Assert.IsTrue(sources.ContainsKey(key), $"the chain tutorial text was not collected: {key}");
            Assert.AreEqual(message, sources[key]);

            #region Internal

            // 連結ゴースト1件へchallenges.json差替え
            // Replace test mod's challenges.json with one chain preview
            void SetChainTutorial()
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
                SetChallengeMaster(master);
            }

            #endregion
        }

        private static void SetChallengeMaster(ChallengeMaster challengeMaster)
        {
            typeof(MasterHolder).GetProperty(nameof(MasterHolder.ChallengeMaster)).GetSetMethod(true).Invoke(null, new object[] { challengeMaster });
        }
    }
}
