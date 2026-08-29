using System.IO;
using System.Linq;
using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Map
{
    public class MapObjectMasterValidationTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 同じmapObject内でminingToolsのtoolItemGuidが重複すると失敗する()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "map.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var miningMapObject = ((JArray)json["mapObjects"]).Children<JObject>()
                .Single(element => (string)element["miningType"] == "Mining");
            var miningTools = (JArray)miningMapObject["miningParam"]["miningTools"];

            // 実在するツール定義を複製し、foreignKey成功と重複失敗を分離する
            // Duplicate a valid tool definition so foreign-key success is isolated from duplicate failure
            miningTools.Add(miningTools[0].DeepClone());
            var master = new MapObjectMaster(json);

            Assert.IsFalse(master.Validate(out var logs));
            StringAssert.Contains("duplicate ToolItemGuid", logs);
        }

        [TestCase("damage", "non-positive Damage")]
        [TestCase("attackSpeed", "non-positive AttackSpeed")]
        public void miningToolsの非正値パラメータは失敗する(string propertyName, string expectedLog)
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "map.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var miningMapObject = ((JArray)json["mapObjects"]).Children<JObject>()
                .Single(element => (string)element["miningType"] == "Mining");

            // 実在定義の検査対象だけを0へ変え、他のマスタ整合性から独立させる
            // Set only the validated field to zero so other master consistency remains intact
            miningMapObject["miningParam"]["miningTools"][0][propertyName] = 0;
            var master = new MapObjectMaster(json);

            Assert.IsFalse(master.Validate(out var logs));
            StringAssert.Contains(expectedLog, logs);
        }

        [Test]
        public void earnItemsが空のmapObjectがあると失敗する()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "map.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var miningMapObject = ((JArray)json["mapObjects"]).Children<JObject>()
                .Single(element => (string)element["miningType"] == "Mining");

            // earnItemsだけを空にする
            // Empty only earnItems on a valid definition
            miningMapObject["earnItems"] = new JArray();
            var master = new MapObjectMaster(json);

            Assert.IsFalse(master.Validate(out var logs));
            StringAssert.Contains("has empty EarnItems", logs);
        }

        [Test]
        public void Noneのmapobjectがearnitemsを持つと失敗する()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "map.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var decoration = ((JArray)json["mapObjects"]).Children<JObject>()
                .Single(element => (string)element["miningType"] == "None");
            var miningMapObject = ((JArray)json["mapObjects"]).Children<JObject>()
                .Single(element => (string)element["miningType"] == "Mining");

            // 実在するearnItemを装飾物へ複製し、foreignKey成功と矛盾失敗を分離する
            // Copy a valid earn item onto the decoration so foreign-key success is isolated from the contradiction failure
            decoration["earnItems"] = miningMapObject["earnItems"].DeepClone();
            var master = new MapObjectMaster(json);

            Assert.IsFalse(master.Validate(out var logs));
            StringAssert.Contains("None must have empty EarnItems", logs);
        }

        [Test]
        public void Noneのmapobjectはearnitemsが空でも成功する()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "map.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var master = new MapObjectMaster(json);

            // Noneの装飾物含みで検証成功
            // Validation succeeds with a None decoration included
            Assert.IsTrue(master.Validate(out var logs), logs);
        }
    }
}
