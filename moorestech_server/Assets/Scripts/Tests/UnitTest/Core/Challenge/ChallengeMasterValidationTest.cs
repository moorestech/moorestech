using System.IO;
using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Challenge
{
    public class ChallengeMasterValidationTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void veinPinが存在しないveinGuidを参照すると失敗する()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "challenges.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var tutorial = (JObject)json["data"][0]["challenges"][0]["tutorials"][0];

            // 他の整合済みfixtureを保ち、vein参照だけを壊して検出責務を分離する
            // Keep the remaining fixture valid and break only the vein reference to isolate this validator
            tutorial["tutorialType"] = "veinPin";
            tutorial["tutorialParam"] = new JObject
            {
                ["veinGuid"] = "99999999-9999-9999-9999-999999999999",
                ["pinText"] = "invalid vein pin",
            };

            var master = new ChallengeMaster(json);

            Assert.IsFalse(master.Validate(out var logs));
            StringAssert.Contains("invalid Tutorial.VeinGuid", logs);
        }

        [Test]
        public void earnItemピンが誰も落とさないアイテムを参照すると失敗する()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "challenges.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var tutorial = (JObject)json["data"][0]["challenges"][0]["tutorials"][0];

            // Test3はitems.jsonに実在するがどのmapObjectのearnItemsにも無い。実在するのに解決先が空になる形を突く
            // Test3 exists in items.json but no mapObject earns it, hitting the "exists yet resolves to nothing" case
            tutorial["tutorialType"] = "mapObjectPin";
            tutorial["tutorialParam"] = new JObject
            {
                ["pinTargetType"] = "earnItem",
                ["pinTargetParam"] = new JObject
                {
                    ["itemGuid"] = "00000000-0000-0000-1234-000000000003",
                },
                ["pinText"] = "nobody drops this",
            };

            var master = new ChallengeMaster(json);

            Assert.IsFalse(master.Validate(out var logs));
            StringAssert.Contains("resolving to no MapObject", logs);
        }

        [Test]
        public void mapObject直指定ピンが装飾物を参照すると失敗する()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "challenges.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var tutorial = (JObject)json["data"][0]["challenges"][0]["tutorials"][0];

            // 装飾物は実在するが狙えない。実在チェックだけでは通ってしまう形を突く
            // The decoration exists yet can never be aimed at, hitting the case an existence check alone lets through
            tutorial["tutorialType"] = "mapObjectPin";
            tutorial["tutorialParam"] = new JObject
            {
                ["pinTargetType"] = "mapObject",
                ["pinTargetParam"] = new JObject
                {
                    ["mapObjectGuid"] = "00000000-0000-4444-0000-000000000001",
                },
                ["pinText"] = "unmineable decoration pin",
            };

            var master = new ChallengeMaster(json);

            Assert.IsFalse(master.Validate(out var logs));
            StringAssert.Contains("which forbids mining", logs);
        }

        [Test]
        public void completeResearchが存在しないresearchNodeGuidを参照すると失敗する()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "challenges.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var taskParam = (JObject)json["data"][0]["challenges"][5]["taskParam"];

            // completeResearchチャレンジ以外のfixtureは保ち、researchNodeGuid参照だけを壊して検出責務を分離する
            // Keep the remaining fixture valid and break only the researchNodeGuid reference to isolate this validator
            taskParam["researchNodeGuid"] = "99999999-9999-9999-9999-999999999999";

            var master = new ChallengeMaster(json);

            Assert.IsFalse(master.Validate(out var logs));
            StringAssert.Contains("invalid TaskParam.ResearchNodeGuid", logs);
        }

        [Test]
        public void uiDragGuideのanchorIdは検証されず素通りする()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "challenges.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var tutorials = (JArray)json["data"][0]["challenges"][5]["tutorials"];

            // anchorIdはWeb側でのみ解決するためマスタ検証は素通り（誤設定は表示されないだけ・設定者責任）
            // Anchor IDs resolve only on the web side; master validation passes them through (missets simply don't render)
            tutorials.Add(new JObject
            {
                ["tutorialGuid"] = "00000000-0000-0000-8901-000000000101",
                ["tutorialType"] = "uiDragGuide",
                ["tutorialParam"] = new JObject
                {
                    ["fromAnchorId"] = "totally-unknown.anchor",
                    ["toAnchorId"] = "hotbar.hud",
                },
            });

            var master = new ChallengeMaster(json);

            Assert.IsTrue(master.Validate(out var logs));
            Assert.IsEmpty(logs);
        }
    }
}
