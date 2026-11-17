using System.Linq;
using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps.V9ToV10;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.UnitTest.Game.SaveLoad.Migration.V9ToV10.V9SaveJsonFixture;

namespace Tests.UnitTest.Game.SaveLoad.Migration.V9ToV10
{
    // 向き読替・重なり撤去・添字付替えを検証
    // Verify renumbering, overlap removal and index remapping
    public class SaveMigrationStepV9ToV10BlueprintTest
    {
        private string _cubeGuid;
        private string _tallGuid;

        [SetUp]
        public void SetUp()
        {
            (_cubeGuid, _tallGuid) = LoadTestMasterGuids();
        }

        // [0] 1×2×3 UpNorth は [1] の (0,1,2) と重なって撤去され、[2] は離れている
        // [0] the 1x2x3 UpNorth overlaps [1] at (0,1,2) and is removed, while [2] stands apart
        [Test]
        public void 重なる縦置きを撤去し電線とチェーンの添字を付け替えるTest()
        {
            var blueprint = Blueprint(
                new JArray(BlueprintBlock(_tallGuid, 0, 0, 0, 0), BlueprintBlock(_cubeGuid, 5, 0, 1, 2), BlueprintBlock(_cubeGuid, 4, 5, 0, 5)),
                new JArray(Line(0, 2), Line(1, 2)),
                new JArray(Line(2, 1)));
            LogAssert.Expect(LogType.Warning, new Regex("ブループリント内の縦向きを寝かせると重なるため撤去"));

            var result = new SaveMigrationStepV9ToV10().Migrate(SaveWith(blueprint));

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var migrated = result.Save["blueprints"][0];
            CollectionAssert.AreEqual(new[] { 1, 0 }, migrated["blocks"].Select(block => block["direction"].Value<int>()).ToArray());
            CollectionAssert.AreEqual(new[] { 5 }, migrated["blocks"].Skip(1).Select(block => block["offsetX"].Value<int>()).ToArray());
            Assert.AreEqual(1, migrated["wires"].Count());
            Assert.AreEqual(0, migrated["wires"][0]["blockIndexA"].Value<int>());
            Assert.AreEqual(1, migrated["wires"][0]["blockIndexB"].Value<int>());
            Assert.AreEqual(1, migrated["chains"][0]["blockIndexA"].Value<int>());
            Assert.AreEqual(0, migrated["chains"][0]["blockIndexB"].Value<int>());
        }

        [Test]
        public void 重ならない縦置きとパネルは寝かせて残し添字も変えないTest()
        {
            var blueprint = Blueprint(
                new JArray(BlueprintBlock(_tallGuid, 11, 0, 0, 0), BlueprintBlock(CleanRoomPanelGuid, 2, 5, 0, 0)),
                new JArray(Line(0, 1)),
                new JArray());

            var result = new SaveMigrationStepV9ToV10().Migrate(SaveWith(blueprint));

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var blocks = result.Save["blueprints"][0]["blocks"];
            CollectionAssert.AreEqual(new[] { 3, 2 }, blocks.Select(block => block["direction"].Value<int>()).ToArray());
            Assert.AreEqual(CleanRoomWallPanelGuid, blocks[1]["blockGuid"].Value<string>());
            Assert.AreEqual(1, result.Save["blueprints"][0]["wires"][0]["blockIndexB"].Value<int>());
        }

        [Test]
        public void ブループリントが無ければ何もしないTest()
        {
            var result = new SaveMigrationStepV9ToV10().Migrate(new JObject { ["worldVersion"] = 9, ["world"] = new JArray() });

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.IsNull(result.Save["blueprints"]);
        }

        [Test]
        public void 電線の添字が範囲外なら変換を拒否し原本を書き換えないTest()
        {
            var save = SaveWith(Blueprint(new JArray(BlueprintBlock(_tallGuid, 0, 0, 0, 0)), new JArray(Line(0, 3)), new JArray()));
            var before = save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex("版9から版10へ変換できません"));

            var result = new SaveMigrationStepV9ToV10().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            Assert.IsTrue(JToken.DeepEquals(before, save));
        }

        private static JObject Blueprint(JArray blocks, JArray wires, JArray chains)
        {
            return new JObject { ["name"] = "bp", ["guid"] = "00000000-0000-0000-0000-0000000000bb", ["blocks"] = blocks, ["wires"] = wires, ["chains"] = chains };
        }

        private static JObject SaveWith(JObject blueprint)
        {
            return new JObject { ["worldVersion"] = 9, ["world"] = new JArray(), ["blueprints"] = new JArray(blueprint) };
        }
    }
}
