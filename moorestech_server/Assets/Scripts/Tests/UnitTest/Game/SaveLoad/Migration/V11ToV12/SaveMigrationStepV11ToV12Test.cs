using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad.Migration.V11ToV12
{
    // 部屋の不純物量が消え、清浄機の摩耗量が経過秒0へ置き換わり、他は残ることを検証
    // Verify room impurity is dropped, purifier wear becomes zero elapsed seconds, and everything else stays
    public class SaveMigrationStepV11ToV12Test
    {
        private const string AirFilterSaveKey = "Game.Block.Blocks.CleanRoom.CleanRoomAirFilterComponent";
        private const string FilterItemGuid = "f4431bb1-35b9-4934-be95-de1641c65fd7";
        private const string AirFilterBlockGuid = "00000000-0000-0000-0000-000000000123";

        [Test]
        public void 部屋の不純物量を消し清浄機の摩耗量を経過秒0へ置き換えるTest()
        {
            var room = new JObject { ["impurityCount"] = 18757.79, ["className"] = "C", ["cells"] = new JArray(new JObject { ["x"] = 1, ["y"] = 2, ["z"] = 3 }) };
            var inventory = new JArray(new JObject { ["itemGuid"] = FilterItemGuid, ["count"] = 100 });
            var airFilterState = new JObject { ["wearAccumulation"] = 14704.55, ["inventory"] = inventory };
            var save = new JObject
            {
                ["worldVersion"] = 11,
                ["world"] = new JArray(WorldBlock(new JObject { [AirFilterSaveKey] = airFilterState })),
                ["cleanRoomRooms"] = new JArray(room),
            };
            var expectedCells = room["cells"].DeepClone();
            var expectedInventory = inventory.DeepClone();
            LogAssert.Expect(LogType.Log, new Regex("版11から版12へ変換しました"));

            var result = new SaveMigrationStepV11ToV12().Migrate(save);

            // 部屋はクラス名とセルを残して不純物量だけ消える
            // The room keeps its class name and cells and loses only the impurity count
            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var migratedRoom = (JObject)result.Save["cleanRoomRooms"][0];
            Assert.IsFalse(migratedRoom.ContainsKey("impurityCount"));
            Assert.AreEqual("C", migratedRoom["className"].Value<string>());
            Assert.IsTrue(JToken.DeepEquals(expectedCells, migratedRoom["cells"]));

            // 清浄機は摩耗量が消えて経過秒0になり、フィルターの在庫は元のまま
            // The purifier loses its wear, gets zero elapsed seconds, and keeps its filter stock
            var migratedFilter = (JObject)result.Save["world"][0]["state"][AirFilterSaveKey];
            Assert.IsFalse(migratedFilter.ContainsKey("wearAccumulation"));
            Assert.AreEqual(0d, migratedFilter["filterWearSeconds"].Value<double>());
            Assert.IsTrue(JToken.DeepEquals(expectedInventory, migratedFilter["inventory"]));
        }

        [Test]
        public void 部屋も清浄機も無いセーブは内容を変えないTest()
        {
            var save = new JObject { ["worldVersion"] = 11, ["world"] = new JArray(WorldBlock(null)) };
            var expected = save.DeepClone();

            var result = new SaveMigrationStepV11ToV12().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(expected, result.Save));
        }

        [Test]
        public void 部屋の一覧が配列でなければ拒否するTest()
        {
            var save = new JObject { ["worldVersion"] = 11, ["world"] = new JArray(), ["cleanRoomRooms"] = "broken" };
            LogAssert.Expect(LogType.Warning, new Regex("版11から版12へ変換できません"));

            var result = new SaveMigrationStepV11ToV12().Migrate(save);

            // world は正しいので、拒否理由は部屋の一覧の形であること
            // The world is valid, so the refusal must come from the rooms list's shape
            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains("cleanRoomRooms", result.FailureReason);
        }

        [Test]
        public void 部屋の一覧と清浄機のstateが明示nullでも成功し触らないTest()
        {
            var save = new JObject
            {
                ["worldVersion"] = 11,
                ["world"] = new JArray(WorldBlock(new JObject { [AirFilterSaveKey] = null })),
                ["cleanRoomRooms"] = null,
            };
            var expected = save.DeepClone();

            var result = new SaveMigrationStepV11ToV12().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(expected, result.Save));
        }

        [Test]
        public void 版番号はステップが触らず連鎖へ任せるTest()
        {
            var save = new JObject { ["worldVersion"] = 11, ["world"] = new JArray() };

            var result = new SaveMigrationStepV11ToV12().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(11, result.Save["worldVersion"].Value<int>());
            Assert.AreEqual(11, new SaveMigrationStepV11ToV12().FromVersion);
        }

        private static JObject WorldBlock(JObject state)
        {
            return new JObject { ["blockGuid"] = AirFilterBlockGuid, ["instanceId"] = 1, ["X"] = 0, ["Y"] = 0, ["Z"] = 0, ["direction"] = 0, ["state"] = state };
        }
    }
}
