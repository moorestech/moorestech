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
    }
}
