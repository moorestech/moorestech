using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad.Migration
{
    public class SaveMigrationStepV7ToV8InheritedLevelTest
    {
        private const string VanillaKey = "Game.Block.Blocks.Machine.VanillaMachineSaveComponent";
        private const string CleanRoomKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";

        [Test]
        public void 通常機械とクリーンルーム機械に継承レベルnullが入り他のキーは残るTest()
        {
            var vanilla = JObject.Parse("{\"inputSlot\":[],\"processor\":{\"state\":1,\"remainingSeconds\":0.5,\"recipeGuid\":\"r\"}}");
            var cleanRoom = JObject.Parse("{\"cycleCount\":3,\"state\":0}");
            var save = Save(
                new JObject { ["state"] = new JObject { [VanillaKey] = vanilla } },
                new JObject { ["state"] = new JObject { [CleanRoomKey] = cleanRoom } });

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var migratedVanilla = (JObject)result.Save["world"][0]["state"][VanillaKey];
            Assert.AreEqual(JTokenType.Null, migratedVanilla["processor"]["inheritedLevel"].Type);
            Assert.AreEqual(JTokenType.Null, migratedVanilla["processor"]["powerMultiplier"].Type);
            Assert.AreEqual(1, migratedVanilla["processor"]["state"].Value<int>());
            Assert.AreEqual("r", migratedVanilla["processor"]["recipeGuid"].Value<string>());
            var migratedCleanRoom = (JObject)result.Save["world"][1]["state"][CleanRoomKey];
            Assert.AreEqual(JTokenType.Null, migratedCleanRoom["inheritedLevel"].Type);
            Assert.AreEqual(JTokenType.Null, migratedCleanRoom["powerMultiplier"].Type);
            Assert.AreEqual(3, migratedCleanRoom["cycleCount"].Value<int>());
        }

        [Test]
        public void 既に継承レベルを持つものと機械以外は素通しされるTest()
        {
            var save = Save(
                JObject.Parse($"{{\"state\":{{\"{VanillaKey}\":{{\"processor\":{{\"inheritedLevel\":3}}}}}}}}"),
                JObject.Parse("{\"state\":{\"ChestComponent\":{\"items\":[]}}}"),
                JObject.Parse("{\"blockGuid\":\"z\"}"),
                JObject.Parse("{\"state\":null}"));

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(3, result.Save["world"][0]["state"][VanillaKey]["processor"]["inheritedLevel"].Value<int>());
            Assert.IsNull(result.Save["world"][1]["state"]["ChestComponent"]["inheritedLevel"]);
        }

        [Test]
        public void processorがオブジェクトでなければ失敗するTest()
        {
            var save = Save(JObject.Parse($"{{\"state\":{{\"{VanillaKey}\":{{\"processor\":5}}}}}}"));

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains("processor", result.FailureReason);
        }

        [Test]
        public void 通常機械のprocessorが明示nullなら飛ばして台数をログに残すTest()
        {
            // 清浄室機械と同じく、ロード側が復元を見送るnullで変換を止めない（判断記録 A18）
            // Like clean-room machines, a null the loader skips must not stop the conversion (decision A18)
            var save = Save(JObject.Parse($"{{\"state\":{{\"{VanillaKey}\":{{\"processor\":null}}}}}}"));
            LogAssert.Expect(LogType.Log, new Regex("processorが null で飛ばした通常機械=1台"));

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(JTokenType.Null, result.Save["world"][0]["state"][VanillaKey]["processor"].Type);
        }

        [TestCase("5")]
        [TestCase("\"broken\"")]
        [TestCase("[]")]
        public void 機械の状態がオブジェクトでなければ失敗するTest(string json)
        {
            var save = Save(new JObject { ["state"] = new JObject { [VanillaKey] = JToken.Parse(json) } });

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains(VanillaKey, result.FailureReason);
        }

        #region TestHelper

        private static JObject Save(params JObject[] blocks)
        {
            return new JObject { ["worldVersion"] = 7, ["world"] = new JArray(blocks) };
        }

        #endregion
    }
}
