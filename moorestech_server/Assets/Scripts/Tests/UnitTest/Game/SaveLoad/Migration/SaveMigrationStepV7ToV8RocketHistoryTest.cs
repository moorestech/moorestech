using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad.Migration
{
    public class SaveMigrationStepV7ToV8RocketHistoryTest
    {
        [Test]
        public void 版7のセーブに未打ち上げの履歴が足され他の節は残るTest()
        {
            // プールの節を先に持たせ、履歴以外の差分が出ないようにする
            // Give the pool section up front so the only difference is the history
            var save = JObject.Parse("{\"worldVersion\":7,\"world\":[{\"blockGuid\":\"x\"}],\"cleanRoomRooms\":[],\"spaceElevatorCablePool\":{\"cableCount\":0}}");
            var original = (JObject)save.DeepClone();

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(JTokenType.Boolean, result.Save["rocketHasLaunchedOnce"].Type);
            Assert.IsFalse(result.Save["rocketHasLaunchedOnce"].Value<bool>());
            var withoutHistory = (JObject)result.Save.DeepClone();
            withoutHistory.Remove("rocketHasLaunchedOnce");
            Assert.IsTrue(JToken.DeepEquals(original, withoutHistory));
        }

        [Test]
        public void nullの履歴は未打ち上げで補うTest()
        {
            var save = JObject.Parse("{\"worldVersion\":7,\"world\":[],\"rocketHasLaunchedOnce\":null}");

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.IsFalse(result.Save["rocketHasLaunchedOnce"].Value<bool>());
        }

        [TestCase(true)]
        [TestCase(false)]
        public void 真偽値の履歴は素通しするTest(bool launched)
        {
            var save = new JObject { ["worldVersion"] = 7, ["world"] = new JArray(), ["rocketHasLaunchedOnce"] = launched };

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(launched, result.Save["rocketHasLaunchedOnce"].Value<bool>());
        }

        [TestCase("1")]
        [TestCase("\"launched\"")]
        [TestCase("{}")]
        [TestCase("[]")]
        public void 真偽値でない履歴は上書きせず変換しないTest(string historyJson)
        {
            var save = JObject.Parse($"{{\"worldVersion\":7,\"world\":[],\"rocketHasLaunchedOnce\":{historyJson}}}");
            var original = (JObject)save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex("版7から版8へ変換できません"));

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains("rocketHasLaunchedOnce", result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(original, save));
        }
    }
}
