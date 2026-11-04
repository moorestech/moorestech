using System.Linq;
using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.UnitTest.Game.SaveLoad.Migration.V9ToV10.V9SaveJsonFixture;

namespace Tests.UnitTest.Game.SaveLoad.Migration.V9ToV10
{
    // 重なり撤去ブロックの巻き添え除去を検証
    // Verify collateral removal for blocks removed by overlap
    public class SaveMigrationStepV9ToV10CollateralTest
    {
        private string _cubeGuid;
        private string _tallGuid;

        [SetUp]
        public void SetUp()
        {
            (_cubeGuid, _tallGuid) = LoadTestMasterGuids();
        }

        // (0,0,0) の 1×2×3 UpNorth は (0,1,2) の水平ブロックと重なって撤去される
        // The 1x2x3 UpNorth at (0,0,0) overlaps the flat block at (0,1,2) and is removed
        [Test]
        public void 撤去したブロックにつながるレール接続と載る列車は外れるTest()
        {
            var save = new JObject
            {
                ["worldVersion"] = 9,
                ["world"] = new JArray(WorldBlock(1, _tallGuid, 0, 0, 0, 0), WorldBlock(2, _cubeGuid, 4, 0, 1, 2), WorldBlock(3, _cubeGuid, 4, 5, 0, 5)),
                ["railSegments"] = new JArray(Segment(Destination(0, 0, 0), Destination(5, 0, 5)), Segment(Destination(5, 0, 5), Destination(9, 0, 9))),
                ["trainUnits"] = new JArray(Train(1, Destination(0, 0, 0)), Train(2, Destination(9, 0, 9))),
            };
            LogAssert.Expect(LogType.Warning, new Regex("重なるため撤去"));

            var result = new SaveMigrationStepV9ToV10().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            CollectionAssert.AreEqual(new[] { 2, 3 }, result.Save["world"].Select(block => block["instanceId"].Value<int>()).ToArray());
            Assert.AreEqual(1, result.Save["railSegments"].Count());
            Assert.AreEqual(9, result.Save["railSegments"][0]["B"]["blockPosition"]["x"].Value<int>());
            Assert.AreEqual(1, result.Save["trainUnits"].Count());
            Assert.AreEqual(2, result.Save["trainUnits"][0]["TrainUnitInstanceId"].Value<int>());
        }

        [Test]
        public void 撤去が無ければレール接続と列車に触れないTest()
        {
            var save = new JObject
            {
                ["worldVersion"] = 9,
                ["world"] = new JArray(WorldBlock(1, _tallGuid, 0, 0, 0, 0)),
                ["railSegments"] = new JArray(Segment(Destination(0, 0, 0), Destination(5, 0, 5))),
                ["trainUnits"] = new JArray(Train(1, Destination(0, 0, 0))),
            };
            var railBefore = save["railSegments"].DeepClone();
            var trainsBefore = save["trainUnits"].DeepClone();

            var result = new SaveMigrationStepV9ToV10().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(railBefore, result.Save["railSegments"]));
            Assert.IsTrue(JToken.DeepEquals(trainsBefore, result.Save["trainUnits"]));
        }

        [Test]
        public void レール接続の節が配列でなければ変換を拒否するTest()
        {
            var save = new JObject { ["worldVersion"] = 9, ["world"] = new JArray(WorldBlock(1, _cubeGuid, 4, 0, 0, 0)), ["railSegments"] = 5 };
            LogAssert.Expect(LogType.Warning, new Regex("版9から版10へ変換できません"));

            var result = new SaveMigrationStepV9ToV10().Migrate(save);

            Assert.IsFalse(result.IsConverted);
        }

        private static JObject Segment(JObject start, JObject end)
        {
            return new JObject { ["A"] = start, ["B"] = end };
        }

        private static JObject Train(int trainUnitInstanceId, JObject railNode)
        {
            return new JObject { ["TrainUnitInstanceId"] = trainUnitInstanceId, ["railPositionSaveData"] = new JObject { ["RailSnapshot"] = new JArray(railNode) } };
        }
    }
}
